using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Protocol.Wire;

namespace CasualtiesUnknownOnline.Application.Kernel;

/// <summary>
/// The state-stream half of the kernel protocol service. Split out of
/// <see cref="KernelProtocolService"/> when the snapshot event-versioning work
/// pushed that file past the architecture line gate; this class owns the
/// high-frequency/stream frames and their per-payload snapshot sequence
/// counters while the kernel service keeps command/checkpoint handling and the
/// shared envelope header.
/// </summary>
internal sealed class KernelStateStreamService(
	IKernelSessionFacts session,
	IKernelFrameSender sender,
	IKernelCheckpointSource checkpoints,
	Func<WirePayloadType, EnvelopeHeader> createHeader)
{
	private readonly IKernelSessionFacts _session = session;
	private readonly IKernelFrameSender _sender = sender;
	private readonly IKernelCheckpointSource _checkpoints = checkpoints;
	private readonly Func<WirePayloadType, EnvelopeHeader> _createHeader = createHeader;
	private uint _nextItemSnapshotSeq;
	private uint _nextWorldItemsSnapshotSeq;

	public void Reset()
	{
		_nextItemSnapshotSeq = 0;
		_nextWorldItemsSnapshotSeq = 0;
	}

	public void SendStateStream(IReadOnlyList<WireItemMoveEntry> itemMoves)
	{
		if (!_session.IsHost || !_session.SessionActive || itemMoves.Count == 0)
		{
			return;
		}

		var frame = new ProtocolFrame
		{
			Kind = EnvelopeKind.StateStream,
			StateStream = new StateStreamEnvelope
			{
				Header = _createHeader(WirePayloadType.StateStream),
				Stream = new WireStateStream
				{
					ItemMoves = [.. itemMoves],
				},
			},
		};
		SendToGuests(frame, reliable: false);
	}

	public void SendStateStreamTo(ulong targetSteamId, WireStateStream stream, WirePayloadType payloadType, bool reliable = false)
	{
		if (!_session.SessionActive || targetSteamId == 0)
		{
			return;
		}

		var frame = CreateStateStreamFrame(stream, payloadType);
		_sender.Send(targetSteamId, frame, reliable);
	}

	public void BroadcastStateStream(WireStateStream stream, WirePayloadType payloadType, bool reliable = false)
	{
		if (!_session.IsHost || !_session.SessionActive)
		{
			return;
		}

		var frame = CreateStateStreamFrame(stream, payloadType);
		SendToGuests(frame, reliable);
	}

	public void BroadcastStateStreamTo(IEnumerable<ulong> targets, WireStateStream stream, WirePayloadType payloadType, bool reliable = false)
	{
		if (!_session.SessionActive)
		{
			return;
		}

		var frame = CreateStateStreamFrame(stream, payloadType);
		_sender.SendToAll(targets, frame, reliable);
	}

	public void SendItemStateStreamTo(ulong targetSteamId, IReadOnlyList<WireWorldItemState> items, WirePayloadType payloadType, bool reliable = true, int layerModifierIndex = 0, byte[]? layerModifierRandomState = null)
	{
		if (!_session.IsHost || !_session.SessionActive || items.Count == 0 || targetSteamId == 0)
		{
			return;
		}

		var frame = CreateItemStateStreamFrame(items, payloadType, layerModifierIndex, layerModifierRandomState);
		_sender.Send(targetSteamId, frame, reliable);
	}

	public void BroadcastItemStateStream(IReadOnlyList<WireWorldItemState> items, WirePayloadType payloadType, bool reliable = false, int layerModifierIndex = 0, byte[]? layerModifierRandomState = null)
	{
		if (!_session.IsHost || !_session.SessionActive || items.Count == 0)
		{
			return;
		}

		var frame = CreateItemStateStreamFrame(items, payloadType, layerModifierIndex, layerModifierRandomState);
		SendToGuests(frame, reliable);
	}

	private ProtocolFrame CreateStateStreamFrame(WireStateStream stream, WirePayloadType payloadType) =>
		new()
		{
			Kind = EnvelopeKind.StateStream,
			StateStream = new StateStreamEnvelope
			{
				Header = _createHeader(payloadType),
				Stream = stream,
			},
		};

	private ProtocolFrame CreateItemStateStreamFrame(IReadOnlyList<WireWorldItemState> items, WirePayloadType payloadType, int layerModifierIndex = 0, byte[]? layerModifierRandomState = null) =>
		new()
		{
			Kind = EnvelopeKind.StateStream,
			StateStream = new StateStreamEnvelope
			{
				Header = _createHeader(payloadType),
				Stream = new WireStateStream
				{
					ItemStates = [.. items],
					LayerModifierIndex = layerModifierIndex,
					LayerModifierRandomState = layerModifierRandomState,
					Seq = NextSnapshotSeq(payloadType),
					BaseGlobalRevision = _checkpoints.CurrentGlobalRevision,
				},
			},
		};

	private void SendToGuests(ProtocolFrame frame, bool reliable)
	{
		foreach (var peerId in _session.HandshakenPeerIds)
		{
			_sender.Send(peerId, frame, reliable);
		}
	}

	private uint NextSnapshotSeq(WirePayloadType payloadType) => payloadType switch
	{
		WirePayloadType.ItemSnapshotStream => ++_nextItemSnapshotSeq,
		WirePayloadType.WorldItemsSnapshotStream => ++_nextWorldItemsSnapshotSeq,
		_ => 0,
	};
}
