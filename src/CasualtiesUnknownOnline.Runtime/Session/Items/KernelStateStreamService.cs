using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// The state-stream half of the kernel protocol service. Split out of
/// <see cref="KernelProtocolService"/> when the snapshot event-versioning work
/// pushed that file past the architecture line gate; this class owns the
/// high-frequency/stream frames and their per-payload snapshot sequence
/// counters while the kernel service keeps command/checkpoint handling and the
/// shared envelope header.
/// </summary>
internal sealed class KernelStateStreamService(
	ISessionControl session,
	PacketSender sender,
	ItemKernelAuthority authority,
	Func<WirePayloadType, EnvelopeHeader> createHeader)
{
	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;
	private readonly ItemKernelAuthority _authority = authority;
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
		if (_session.Role != SessionRole.Host || !_session.SessionActive || itemMoves.Count == 0)
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
		_sender.Send(targetSteamId, NetMsg.KernelEnvelope, frame, reliable);
	}

	public void BroadcastStateStream(WireStateStream stream, WirePayloadType payloadType, bool reliable = false)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
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
		_sender.SendToAll(targets, NetMsg.KernelEnvelope, frame, reliable);
	}

	public void SendItemStateStreamTo(ulong targetSteamId, IReadOnlyList<WireWorldItemState> items, WirePayloadType payloadType, bool reliable = true, int layerModifierIndex = 0, byte[]? layerModifierRandomState = null)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || items.Count == 0 || targetSteamId == 0)
		{
			return;
		}

		var frame = CreateItemStateStreamFrame(items, payloadType, layerModifierIndex, layerModifierRandomState);
		_sender.Send(targetSteamId, NetMsg.KernelEnvelope, frame, reliable);
	}

	public void BroadcastItemStateStream(IReadOnlyList<WireWorldItemState> items, WirePayloadType payloadType, bool reliable = false, int layerModifierIndex = 0, byte[]? layerModifierRandomState = null)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || items.Count == 0)
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
					BaseGlobalRevision = _authority.CurrentGlobalRevision,
				},
			},
		};

	private void SendToGuests(ProtocolFrame frame, bool reliable)
	{
		foreach (var member in _session.Members.Where(m => m.Handshaken && m.SteamId != _session.LocalSteamId))
		{
			_sender.Send(member.SteamId, NetMsg.KernelEnvelope, frame, reliable);
		}
	}

	private uint NextSnapshotSeq(WirePayloadType payloadType) => payloadType switch
	{
		WirePayloadType.ItemSnapshotStream => ++_nextItemSnapshotSeq,
		WirePayloadType.WorldItemsSnapshotStream => ++_nextWorldItemsSnapshotSeq,
		_ => 0,
	};
}
