using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.Protocol.Versioning;
using CasualtiesUnknownOnline.Protocol.Wire;
using Microsoft.Extensions.Logging;
using System.Threading;

namespace CasualtiesUnknownOnline.Application.Kernel;

/// <summary>
/// Phase C kernel protocol service. It rides the existing transport as one
/// one kernel-envelope frame whose payload is a
/// <see cref="ProtocolFrame"/>. On the host it executes decoded commands and
/// broadcasts committed batches; on the guest it restores checkpoints and
/// applies committed batches to the replay kernel.
/// </summary>
public sealed class KernelProtocolService : IKernelProtocolControl, IDisposable
{
	private const int JournalCapacity = 2048;

	private readonly IKernelSessionFacts _session;
	private readonly IKernelFrameSender _sender;
	private readonly IKernelCheckpointSource _checkpointSource;
	private readonly IKernelBatchApplication _batches;
	private readonly RefusedItemCreations _refusedCreations;
	private readonly IKernelPendingCommands _pendingCommands;
	private readonly IKernelWireCodec _codec;
	private readonly ILogger<KernelProtocolService> _log;
	private readonly KernelProtocolCommandHandler _commandHandler;
	private readonly List<CommittedBatch> _journal = [];
	private readonly Dictionary<ulong, CommittedBatch> _pendingBatches = [];
	private readonly KernelStateStreamService _stateStreams;
	private readonly GuestCheckpointReceiver _checkpointReceiver;
	private readonly HashSet<ulong> _staleStreamEpochWarned = [];
	private long _nextMessageId;

	public event Action<IReadOnlyList<WireItemMoveEntry>>? ItemMovesReceived;

	public event Action<WirePayloadType, WireStateStream>? ItemStateStreamReceived;

	public event Action<ulong, WirePayloadType, WireStateStream>? EntityStateStreamReceived;

	public event Action<ulong, RejectionReason>? CommandRejected;

	public KernelProtocolService(
		IKernelSessionFacts session,
		IKernelFrameSender sender,
		IKernelItemFacts items,
		IKernelCommandExecution execution,
		IKernelCheckpointSource checkpointSource,
		IKernelBatchApplication batches,
		RefusedItemCreations refusedCreations,
		IKernelPendingCommands pendingCommands,
		KernelCommandGateway gateway,
		IKernelWireCodec codec,
		ILogger<KernelProtocolService> log)
	{
		_session = session;
		_sender = sender;
		_checkpointSource = checkpointSource;
		_batches = batches;
		_refusedCreations = refusedCreations;
		_pendingCommands = pendingCommands;
		_codec = codec;
		_log = log;
		_stateStreams = new KernelStateStreamService(session, sender, checkpointSource, payloadType => CreateHeader(payloadType, 0));
		_checkpointReceiver = new GuestCheckpointReceiver(batches, codec, log);
		_commandHandler = new KernelProtocolCommandHandler(session, sender, items, execution, checkpointSource, refusedCreations, gateway, codec, log);
		_batches.BatchCommitted += BroadcastCommittedBatch;
		_session.SessionEnded += ResetSessionState;
	}

	public void Dispose()
	{
		_batches.BatchCommitted -= BroadcastCommittedBatch;
		_session.SessionEnded -= ResetSessionState;
	}

	public void BroadcastCommittedBatch(CommittedBatch batch)
	{
		if (!_session.IsHost || !_session.SessionActive)
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
				Batch = _codec.ToWireBatch(batch),
			},
		};

		SendToGuests(frame);
	}

	public void SendCheckpoint(ulong targetSteamId)
	{
		if (!_session.IsHost || !_session.SessionActive)
		{
			return;
		}

		var checkpoint = _checkpointSource.CreateCheckpoint();
		var chunks = WireCheckpointAssembler.Split(checkpoint, _codec);
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
			_sender.Send(targetSteamId, frame);
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
					Batch = _codec.ToWireBatch(batch),
				},
			};
			_sender.Send(targetSteamId, frame);
		}

		_log.LogInformation("Sent kernel checkpoint at revision {Revision} to {Target} with {Journal} tail batch(es).",
			checkpoint.GlobalRevision, targetSteamId, _journal.Count(b => b.GlobalRevision > checkpoint.GlobalRevision));
	}

	public ulong CurrentRunEpoch => _checkpointSource.CurrentRunEpoch.Value;

	public void AdoptHostRunEpoch(ulong runEpoch) => _checkpointReceiver.AdoptHostRunEpoch(runEpoch);

	public void SendCommand(WireCommand command, WirePayloadType payloadType)
	{
		if (!_session.IsGuest || !_session.SessionActive || _session.HostSteamId == 0)
		{
			return;
		}

		var messageId = (ulong)Interlocked.Increment(ref _nextMessageId);
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
		// The unacknowledged-report window (audit row I5) re-sends this exact frame
		// until the host's verdict arrives. Registered BEFORE the send: a transport that
		// dispatches inline (the simulation harness does) can deliver the verdict inside
		// the send call, and one that arrived before the registration would be lost,
		// leaving the window open on a report the host has already judged. Tracked even
		// when the transport refuses the send, because the window's first repeat is what
		// heals a send that never left.
		_pendingCommands.Track(command, header.OperationId, frame, payloadType);
		_sender.TrySend(_session.HostSteamId, frame);
	}

	public void SendStateStream(IReadOnlyList<WireItemMoveEntry> itemMoves) => _stateStreams.SendStateStream(itemMoves);

	public void SendStateStreamTo(ulong targetSteamId, WireStateStream stream, WirePayloadType payloadType, bool reliable = false) =>
		_stateStreams.SendStateStreamTo(targetSteamId, stream, payloadType, reliable);

	public void BroadcastStateStream(WireStateStream stream, WirePayloadType payloadType, bool reliable = false) =>
		_stateStreams.BroadcastStateStream(stream, payloadType, reliable);

	public void BroadcastStateStreamTo(IEnumerable<ulong> targets, WireStateStream stream, WirePayloadType payloadType, bool reliable = false) =>
		_stateStreams.BroadcastStateStreamTo(targets, stream, payloadType, reliable);

	public void SendItemStateStreamTo(ulong targetSteamId, IReadOnlyList<WireWorldItemState> items, WirePayloadType payloadType, bool reliable = true, int layerModifierIndex = 0, byte[]? layerModifierRandomState = null) =>
		_stateStreams.SendItemStateStreamTo(targetSteamId, items, payloadType, reliable, layerModifierIndex, layerModifierRandomState);

	public void BroadcastItemStateStream(IReadOnlyList<WireWorldItemState> items, WirePayloadType payloadType, bool reliable = false, int layerModifierIndex = 0, byte[]? layerModifierRandomState = null) =>
		_stateStreams.BroadcastItemStateStream(items, payloadType, reliable, layerModifierIndex, layerModifierRandomState);

	public void HandleFrame(ulong sender, ProtocolFrame frame)
	{
		if (frame is null)
		{
			_log.LogWarning("Dropped null kernel protocol frame from {Sender}.", sender);
			return;
		}

		if (!ProtocolFrameValidator.TryValidate(frame, sender, out var validationError))
		{
			_log.LogWarning("Dropped invalid kernel protocol frame from {Sender}: {Reason}.",
				sender, validationError);
			return;
		}

		if (_session.IsHost)
		{
			HandleHostFrame(sender, frame);
		}
		else if (_session.IsGuest)
		{
			HandleGuestFrame(sender, frame);
		}
		else
		{
			_log.LogWarning("Dropped kernel protocol frame from {Sender} while role is {Role}.", sender, _session.RoleName);
		}
	}

	public void SendCommandRejected(ulong targetSteamId, ulong itemId, RejectionReason reason) => _commandHandler.SendCommandRejected(targetSteamId, itemId, reason);

	public void ResetSessionState()
	{
		_batches.ResetSessionState();
		_journal.Clear();
		_checkpointReceiver.ResetSessionState();
		_pendingBatches.Clear();
		_nextMessageId = 0;
		_staleStreamEpochWarned.Clear();
		_stateStreams.Reset();
		_refusedCreations.Reset(); // item ids are session-partitioned: a finished session's creation tombstones must not refuse the next session's ids
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
				_log.LogWarning("Dropped unexpected {Kind} envelope from guest {Sender}.", frame.Kind, sender);
				break;
			case EnvelopeKind.StateStream when frame.StateStream is not null:
				HandleStateStream(sender, frame.StateStream);
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
				_checkpointReceiver.HandleChunk(sender, frame.Checkpoint);
				break;
			case EnvelopeKind.CommittedBatch when frame.CommittedBatch is not null:
				HandleCommittedBatch(sender, frame.CommittedBatch);
				break;
			case EnvelopeKind.Command when frame.Command is not null && frame.Command.Header.PayloadType == WirePayloadType.CommandRejected:
				HandleCommandRejected(frame.Command);
				break;
			case EnvelopeKind.Command:
				_log.LogWarning("Dropped command envelope from host {Sender}.", sender);
				break;
			case EnvelopeKind.StateStream when frame.StateStream is not null:
				HandleStateStream(sender, frame.StateStream);
				break;
			default:
				_log.LogWarning("Dropped unknown kernel envelope kind {Kind} from {Sender}.", frame.Kind, sender);
				break;
		}
	}

	private void HandleCommandRejected(CommandEnvelope envelope)
	{
		var itemId = envelope.Command.Identity.InstanceId;
		var reason = (RejectionReason)envelope.Command.RejectionReason;
		// The host judged this item's operation and refused it (audit row I5): the
		// re-report window for the item closes; the refusal itself reaches the item
		// domain's rollback path below exactly as it always did.
		_pendingCommands.ClearRejected(itemId);
		_log.LogWarning("Kernel command rejected by host for item {ItemId}: {Reason}.", itemId, reason);
		CommandRejected?.Invoke(itemId, reason);
	}

	private void HandleStateStream(ulong sender, StateStreamEnvelope envelope)
	{
		// Old-run streams must not pollute the new run. The stream sequence spaces
		// reset per session, so a late frame from the previous run would otherwise
		// look fresh; the header epoch is the authoritative filter (glossary:
		// "all old-epoch commands, batches, and stream packets are rejected").
		var currentEpoch = _checkpointSource.CurrentRunEpoch.Value;
		if (envelope.Header.RunEpoch != currentEpoch)
		{
			// Warn once per sender: a 20 Hz stream from a mismatched epoch would
			// otherwise flood the log for the rest of the connection.
			if (_staleStreamEpochWarned.Add(sender))
			{
				_log.LogWarning("Dropped state stream with stale run epoch {Epoch} from {Sender} (current {Current}); further drops for this peer log at debug.",
					envelope.Header.RunEpoch, sender, currentEpoch);
			}
			else
			{
				_log.LogDebug("Dropped state stream with stale run epoch {Epoch} from {Sender} (current {Current}).",
					envelope.Header.RunEpoch, sender, currentEpoch);
			}

			return;
		}

		if (envelope.Stream.ItemMoves.Count > 0)
		{
			ItemMovesReceived?.Invoke(envelope.Stream.ItemMoves);
		}

		if (envelope.Stream.ItemStates.Count > 0)
		{
			ItemStateStreamReceived?.Invoke(envelope.Header.PayloadType, envelope.Stream);
		}

		if (envelope.Stream.PlayerStates.Count > 0 || envelope.Stream.EnemyStates.Count > 0)
		{
			EntityStateStreamReceived?.Invoke(sender, envelope.Header.PayloadType, envelope.Stream);
		}
	}

	private void HandleCommand(ulong sender, CommandEnvelope envelope)
	{
		var currentEpoch = _checkpointSource.CreateCheckpoint().RunEpoch.Value;
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

		_commandHandler.Handle(sender, envelope);
	}

	private void HandleCommittedBatch(ulong sender, CommittedBatchEnvelope envelope)
	{
		var batch = _codec.FromWireBatch(envelope.Batch, _checkpointSource.CreateCheckpoint().RunEpoch);
		if (batch.RunEpoch.Value != _checkpointSource.CreateCheckpoint().RunEpoch.Value)
		{
			_log.LogWarning("Batch from {Sender} has epoch {Epoch}; current is {Current} — dropped.",
				sender, batch.RunEpoch.Value, _checkpointSource.CreateCheckpoint().RunEpoch.Value);
			return;
		}

		// This guest's own command has been judged (audit row I5): the window closes at
		// RECEIPT, before the revision guard below, because the host answers a re-report
		// with the ORIGINAL batch — a revision this side may already hold.
		_pendingCommands.ClearCommitted(batch.OperationId.Value);

		var expected = _checkpointSource.CurrentGlobalRevision + 1;
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
		if (batch.GlobalRevision < _checkpointSource.CurrentGlobalRevision + 1)
		{
			return;
		}

		var result = _batches.Apply(batch);
		if (!result.Success)
		{
			_log.LogWarning("Applying batch from {Sender} failed: {Message}", sender, result.Error);
			return;
		}

		_log.LogDebug("Applied kernel batch {Operation} revision {Revision} from {Sender}.",
			batch.OperationId.Value, batch.GlobalRevision, sender);

		while (_pendingBatches.TryGetValue(_checkpointSource.CurrentGlobalRevision + 1, out var next))
		{
			_pendingBatches.Remove(_checkpointSource.CurrentGlobalRevision + 1);
			var nextResult = _batches.Apply(next);
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
		if (!_session.IsGuest || !_session.SessionActive || _session.HostSteamId == 0)
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
				Batch = _codec.ToWireBatch(batch),
			},
		};
		_sender.Send(targetSteamId, frame);
	}

	private void SendToGuests(ProtocolFrame frame, bool reliable = true)
	{
		foreach (var peerId in _session.HandshakenPeerIds)
		{
			_sender.Send(peerId, frame, reliable);
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
			RunEpoch = batch?.RunEpoch.Value ?? _checkpointSource.CreateCheckpoint().RunEpoch.Value,
			SenderId = _session.LocalSteamId,
			MessageId = (ulong)Interlocked.Increment(ref _nextMessageId),
			OperationId = operationId,
			BaseGlobalRevision = baseRevision ?? (batch is null ? 0 : batch.GlobalRevision - 1),
			PayloadType = payloadType,
		};
}
