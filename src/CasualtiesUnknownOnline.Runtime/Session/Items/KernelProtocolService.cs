using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Protocol.Versioning;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// Phase C kernel protocol service. It rides the existing transport as one
/// <see cref="NetMsg.KernelEnvelope"/> frame whose payload is a
/// <see cref="ProtocolFrame"/>. On the host it executes decoded commands and
/// broadcasts committed batches; on the guest it restores checkpoints and
/// applies committed batches to the replay kernel.
/// </summary>
public sealed class KernelProtocolService : IKernelProtocolControl, IDisposable
{
	private const int JournalCapacity = 2048;

	private readonly ISessionControl _session;
	private readonly PacketSender _sender;
	private readonly ItemKernelAuthority _authority;
	private readonly ILogger<KernelProtocolService> _log;
	private readonly List<CommittedBatch> _journal = [];
	private readonly Dictionary<int, WireCheckpoint> _checkpointChunks = [];
	private readonly Dictionary<ulong, CommittedBatch> _pendingBatches = [];
	private long _nextMessageId;

	public event Action<IReadOnlyList<WireItemMoveEntry>>? ItemMovesReceived;

	public KernelProtocolService(
		ISessionControl session,
		PacketSender sender,
		ItemKernelAuthority authority,
		ILogger<KernelProtocolService> log)
	{
		_session = session;
		_sender = sender;
		_authority = authority;
		_log = log;
		_authority.BatchCommitted += BroadcastCommittedBatch;
		_session.SessionEnded += ResetForSessionEnd;
	}

	public void Dispose()
	{
		_authority.BatchCommitted -= BroadcastCommittedBatch;
		_session.SessionEnded -= ResetForSessionEnd;
	}

	public void BroadcastCommittedBatch(CommittedBatch batch)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		_journal.Add(batch);
		if (_journal.Count > JournalCapacity)
		{
			_journal.RemoveRange(0, _journal.Count - JournalCapacity);
		}

		var frame = new ProtocolFrame
		{
			Kind = EnvelopeKind.CommittedBatch,
			CommittedBatch = new CommittedBatchEnvelope
			{
				Header = CreateHeader(WirePayloadType.CommittedBatch, batch.OperationId.Value, batch),
				Batch = KernelWireMapper.ToWireBatch(batch),
			},
		};

		SendToGuests(frame);
	}

	public void SendCheckpoint(ulong targetSteamId)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		var checkpoint = _authority.CreateCheckpoint();
		var chunks = WireCheckpointAssembler.Split(checkpoint);
		foreach (var chunk in chunks)
		{
			var frame = new ProtocolFrame
			{
				Kind = EnvelopeKind.Checkpoint,
				Checkpoint = new CheckpointEnvelope
				{
					Header = CreateHeader(WirePayloadType.CheckpointChunk, 0, null, checkpoint.GlobalRevision),
					Checkpoint = chunk,
				},
			};
			_sender.Send(targetSteamId, NetMsg.KernelEnvelope, frame);
		}

		foreach (var batch in _journal)
		{
			if (batch.GlobalRevision <= checkpoint.GlobalRevision)
			{
				continue;
			}

			var frame = new ProtocolFrame
			{
				Kind = EnvelopeKind.CommittedBatch,
				CommittedBatch = new CommittedBatchEnvelope
				{
					Header = CreateHeader(WirePayloadType.CommittedBatch, batch.OperationId.Value, batch),
					Batch = KernelWireMapper.ToWireBatch(batch),
				},
			};
			_sender.Send(targetSteamId, NetMsg.KernelEnvelope, frame);
		}

		_log.LogInformation("Sent kernel checkpoint at revision {Revision} to {Target} with {Journal} tail batch(es).",
			checkpoint.GlobalRevision, targetSteamId, _journal.Count(b => b.GlobalRevision > checkpoint.GlobalRevision));
	}

	public void SendCommand(WireCommand command, WirePayloadType payloadType)
	{
		if (_session.Role != SessionRole.Guest || !_session.SessionActive || _session.HostSteamId == 0)
		{
			return;
		}

		var messageId = (ulong)System.Threading.Interlocked.Increment(ref _nextMessageId);
		var header = CreateHeader(payloadType, EncodeOperationId(_session.LocalSteamId, messageId));
		var frame = new ProtocolFrame
		{
			Kind = EnvelopeKind.Command,
			Command = new CommandEnvelope
			{
				Header = header,
				Command = command,
			},
		};
		_sender.Send(_session.HostSteamId, NetMsg.KernelEnvelope, frame);
	}

	public void SendStateStream(IReadOnlyList<WireItemMoveEntry> itemMoves)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || itemMoves.Count == 0)
		{
			return;
		}

		var frame = new ProtocolFrame
		{
			Kind = EnvelopeKind.StateStream,
			StateStream = new StateStreamEnvelope
			{
				Header = CreateHeader(WirePayloadType.StateStream, 0),
				Stream = new WireStateStream
				{
					ItemMoves = [.. itemMoves],
				},
			},
		};
		SendToGuests(frame, reliable: false);
	}

	public void HandleFrame(ulong sender, ProtocolFrame frame)
	{
		if (frame is null)
		{
			_log.LogWarning("Dropped null kernel protocol frame from {Sender}.", sender);
			return;
		}

		if (!IsSupportedFrame(frame))
		{
			_log.LogWarning("Dropped unsupported kernel protocol frame from {Sender} ({Kind}).",
				sender, frame.Kind);
			return;
		}

		if (_session.Role == SessionRole.Host)
		{
			HandleHostFrame(sender, frame);
		}
		else if (_session.Role == SessionRole.Guest)
		{
			HandleGuestFrame(sender, frame);
		}
		else
		{
			_log.LogWarning("Dropped kernel protocol frame from {Sender} while role is {Role}.", sender, _session.Role);
		}
	}

	public void ResetForSessionEnd()
	{
		_journal.Clear();
		_checkpointChunks.Clear();
		_pendingBatches.Clear();
		_nextMessageId = 0;
	}

	private bool IsSupportedFrame(ProtocolFrame frame)
	{
		var header = frame.Command?.Header
			?? frame.CommittedBatch?.Header
			?? frame.Checkpoint?.Header
			?? frame.StateStream?.Header;
		if (header is null)
		{
			return false;
		}

		if (header.ProtocolVersion != ProtocolConstants.EnvelopeVersion)
		{
			return false;
		}

		if (!Enum.IsDefined(typeof(WirePayloadType), header.PayloadType))
		{
			return (int)header.PayloadType >= ProtocolConstants.PresentationPayloadStart;
		}

		return true;
	}

	private void HandleHostFrame(ulong sender, ProtocolFrame frame)
	{
		switch (frame.Kind)
		{
			case EnvelopeKind.Command when frame.Command is not null:
				HandleCommand(sender, frame.Command);
				break;
			case EnvelopeKind.CommittedBatch:
			case EnvelopeKind.Checkpoint:
			case EnvelopeKind.StateStream:
				_log.LogWarning("Dropped unexpected {Kind} envelope from guest {Sender}.", frame.Kind, sender);
				break;
			default:
				_log.LogWarning("Dropped unknown kernel envelope kind {Kind} from {Sender}.", frame.Kind, sender);
				break;
		}
	}

	private void HandleGuestFrame(ulong sender, ProtocolFrame frame)
	{
		switch (frame.Kind)
		{
			case EnvelopeKind.Checkpoint when frame.Checkpoint is not null:
				HandleCheckpoint(frame.Checkpoint);
				break;
			case EnvelopeKind.CommittedBatch when frame.CommittedBatch is not null:
				HandleCommittedBatch(sender, frame.CommittedBatch);
				break;
			case EnvelopeKind.Command:
				_log.LogWarning("Dropped command envelope from host {Sender}.", sender);
				break;
			case EnvelopeKind.StateStream when frame.StateStream is not null:
				HandleStateStream(frame.StateStream);
				break;
			default:
				_log.LogWarning("Dropped unknown kernel envelope kind {Kind} from {Sender}.", frame.Kind, sender);
				break;
		}
	}

	private void HandleStateStream(StateStreamEnvelope envelope)
	{
		if (envelope.Stream.ItemMoves.Count == 0)
		{
			return;
		}

		ItemMovesReceived?.Invoke(envelope.Stream.ItemMoves);
	}

	private void HandleCommand(ulong sender, CommandEnvelope envelope)
	{
		var currentEpoch = _authority.CreateCheckpoint().RunEpoch.Value;
		if (envelope.Header.RunEpoch != currentEpoch)
		{
			_log.LogWarning("Command from {Sender} has epoch {Epoch}; current is {Current} — dropped.",
				sender, envelope.Header.RunEpoch, currentEpoch);
			return;
		}

		if (envelope.Command.Kind == WireCommandKind.RangeRequest)
		{
			HandleRangeRequest(sender, envelope.Command);
			return;
		}

		var command = ResolveCommandRevision(KernelWireMapper.FromWireCommand(envelope.Command, envelope.Header));
		if (!_authority.TryExecuteCommand(command, sender, out var batch, out var rejection))
		{
			_log.LogWarning("Kernel command from {Sender} rejected: {Reason} ({Message}).",
				sender, rejection!.Reason, rejection.Message);
			return;
		}

		BroadcastCommittedBatch(batch!);
	}

	private GameCommand ResolveCommandRevision(GameCommand command)
	{
		switch (command)
		{
			case PickUpItemCommand c:
				{
					var current = FindRevision(c.InstanceId);
					if (current is not null)
					{
						return c with { ExpectedRevision = current.Value.Revision };
					}

					break;
				}
			case DropItemCommand c:
				{
					var current = FindRevision(c.InstanceId);
					if (current is not null)
					{
						return c with { ExpectedRevision = current.Value.Revision };
					}

					break;
				}
			case DestroyItemCommand c:
				{
					var current = FindRevision(c.InstanceId);
					if (current is not null)
					{
						return c with { ExpectedRevision = current.Value.Revision };
					}

					break;
				}
			case UpdateItemStateCommand c:
				{
					var current = FindRevision(c.InstanceId);
					if (current is not null)
					{
						return c with { ExpectedRevision = current.Value.Revision };
					}

					break;
				}
			case TransferItemCommand c:
				{
					var current = FindRevision(c.InstanceId);
					if (current is not null)
					{
						return c with { ExpectedRevision = current.Value.Revision };
					}

					break;
				}
		}

		return command;
	}

	private ItemState? FindRevision(ulong itemId) => _authority.FindItem(itemId);

	private void HandleCommittedBatch(ulong sender, CommittedBatchEnvelope envelope)
	{
		var batch = KernelWireMapper.FromWireBatch(envelope.Batch, _authority.CreateCheckpoint().RunEpoch);
		if (batch.RunEpoch.Value != _authority.CreateCheckpoint().RunEpoch.Value)
		{
			_log.LogWarning("Batch from {Sender} has epoch {Epoch}; current is {Current} — dropped.",
				sender, batch.RunEpoch.Value, _authority.CreateCheckpoint().RunEpoch.Value);
			return;
		}

		var expected = _authority.CurrentGlobalRevision + 1;
		if (batch.GlobalRevision > expected)
		{
			_log.LogWarning("Batch from {Sender} creates a revision gap: expected {Expected}, received {Received} — buffering and requesting range.",
				sender, expected, batch.GlobalRevision);
			_pendingBatches[batch.GlobalRevision] = batch;
			RequestRange(expected, batch.GlobalRevision - 1);
			return;
		}

		ApplyBatchAndDrain(batch, sender);
	}

	private void ApplyBatchAndDrain(CommittedBatch batch, ulong sender)
	{
		if (batch.GlobalRevision < _authority.CurrentGlobalRevision + 1)
		{
			return;
		}

		var result = _authority.Apply(batch);
		if (!result.Success)
		{
			_log.LogWarning("Applying batch from {Sender} failed: {Message}", sender, result.Error);
			return;
		}

		_log.LogDebug("Applied kernel batch {Operation} revision {Revision} from {Sender}.",
			batch.OperationId.Value, batch.GlobalRevision, sender);

		while (_pendingBatches.TryGetValue(_authority.CurrentGlobalRevision + 1, out var next))
		{
			_pendingBatches.Remove(_authority.CurrentGlobalRevision + 1);
			var nextResult = _authority.Apply(next);
			if (!nextResult.Success)
			{
				_log.LogWarning("Applying buffered kernel batch {Operation} revision {Revision} failed: {Message}",
					next.OperationId.Value, next.GlobalRevision, nextResult.Error);
				break;
			}

			_log.LogDebug("Applied buffered kernel batch {Operation} revision {Revision}.",
				next.OperationId.Value, next.GlobalRevision);
		}
	}

	private void RequestRange(ulong start, ulong end)
	{
		if (_session.Role != SessionRole.Guest || !_session.SessionActive || _session.HostSteamId == 0)
		{
			return;
		}

		SendCommand(new WireCommand
		{
			Kind = WireCommandKind.RangeRequest,
			RangeStart = start,
			RangeEnd = end,
		}, WirePayloadType.RangeRequestCommand);
	}

	private void HandleRangeRequest(ulong sender, WireCommand command)
	{
		var start = command.RangeStart;
		var end = command.RangeEnd;
		if (start > end || start == 0)
		{
			_log.LogWarning("Ignoring invalid range request from {Sender}: {Start}..{End}.", sender, start, end);
			return;
		}

		if (_journal.Count == 0)
		{
			SendCheckpoint(sender);
			return;
		}

		var first = _journal[0].GlobalRevision;
		var last = _journal[_journal.Count - 1].GlobalRevision;
		if (start < first || end > last)
		{
			_log.LogInformation("Range request {Start}..{End} from {Sender} is outside journal {First}..{Last} — sending fresh checkpoint.",
				start, end, sender, first, last);
			SendCheckpoint(sender);
			return;
		}

		var batches = _journal.Where(b => b.GlobalRevision >= start && b.GlobalRevision <= end).ToList();
		if (batches.Count == 0)
		{
			SendCheckpoint(sender);
			return;
		}

		foreach (var batch in batches)
		{
			SendBatchTo(sender, batch);
		}

		_log.LogInformation("Sent {Count} journal batch(es) {Start}..{End} to {Sender}.",
			batches.Count, start, end, sender);
	}

	private void SendBatchTo(ulong targetSteamId, CommittedBatch batch)
	{
		var frame = new ProtocolFrame
		{
			Kind = EnvelopeKind.CommittedBatch,
			CommittedBatch = new CommittedBatchEnvelope
			{
				Header = CreateHeader(WirePayloadType.CommittedBatch, batch.OperationId.Value, batch),
				Batch = KernelWireMapper.ToWireBatch(batch),
			},
		};
		_sender.Send(targetSteamId, NetMsg.KernelEnvelope, frame);
	}

	private void HandleCheckpoint(CheckpointEnvelope envelope)
	{
		var chunk = envelope.Checkpoint;
		_checkpointChunks[chunk.ChunkIndex] = chunk;
		if (_checkpointChunks.Count != chunk.ChunkCount)
		{
			return;
		}

		try
		{
			var checkpoint = WireCheckpointAssembler.Assemble([.. _checkpointChunks.Values]);
			var result = _authority.Restore(checkpoint);
			if (result.Success)
			{
				_log.LogInformation("Restored kernel checkpoint at revision {Revision} ({Items} items).",
					checkpoint.GlobalRevision, checkpoint.Items.Count);
				_checkpointChunks.Clear();
			}
			else
			{
				_log.LogWarning("Kernel checkpoint restore failed: {Message}", result.Error);
			}
		}
		catch (Exception ex)
		{
			_log.LogWarning(ex, "Kernel checkpoint assembly/restore failed for guest.");
			_checkpointChunks.Clear();
		}
	}

	private void SendToGuests(ProtocolFrame frame, bool reliable = true)
	{
		foreach (var member in _session.Members.Where(m => m.Handshaken && m.SteamId != _session.LocalSteamId))
		{
			_sender.Send(member.SteamId, NetMsg.KernelEnvelope, frame, reliable);
		}
	}

	private static ulong EncodeOperationId(ulong sender, ulong counter)
	{
		var senderLow = (uint)(sender ^ (sender >> 32));
		return ((ulong)senderLow << 32) | (counter & 0xFFFFFFFF);
	}

	private EnvelopeHeader CreateHeader(WirePayloadType payloadType, ulong operationId, CommittedBatch? batch = null, ulong? baseRevision = null) =>
		new()
		{
			ProtocolVersion = ProtocolConstants.EnvelopeVersion,
			RunEpoch = batch?.RunEpoch.Value ?? _authority.CreateCheckpoint().RunEpoch.Value,
			SenderId = _session.LocalSteamId,
			MessageId = (ulong)System.Threading.Interlocked.Increment(ref _nextMessageId),
			OperationId = operationId,
			BaseGlobalRevision = baseRevision ?? (batch is null ? 0 : batch.GlobalRevision - 1),
			PayloadType = payloadType,
		};
}
