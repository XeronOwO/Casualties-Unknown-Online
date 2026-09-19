using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.Protocol.Wire;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// Control surface for the Phase C four-envelope kernel protocol. The packet
/// handler and the item domain use this narrow interface; the ownership and
/// wiring live in <see cref="KernelProtocolService"/>.
/// </summary>
public interface IKernelProtocolControl
{
	void HandleFrame(ulong sender, ProtocolFrame frame);

	void SendCommand(WireCommand command, WirePayloadType payloadType);

	void SendCommandRejected(ulong targetSteamId, ulong itemId, RejectionReason reason);

	void SendStateStream(IReadOnlyList<WireItemMoveEntry> itemMoves);

	void SendItemStateStreamTo(ulong targetSteamId, IReadOnlyList<WireWorldItemState> items, WirePayloadType payloadType, bool reliable = true, int layerModifierIndex = 0, byte[]? layerModifierRandomState = null);

	void BroadcastItemStateStream(IReadOnlyList<WireWorldItemState> items, WirePayloadType payloadType, bool reliable = false, int layerModifierIndex = 0, byte[]? layerModifierRandomState = null);

	void SendStateStreamTo(ulong targetSteamId, WireStateStream stream, WirePayloadType payloadType, bool reliable = false);

	void BroadcastStateStream(WireStateStream stream, WirePayloadType payloadType, bool reliable = false);

	void BroadcastStateStreamTo(IEnumerable<ulong> targets, WireStateStream stream, WirePayloadType payloadType, bool reliable = false);

	void BroadcastCommittedBatch(CommittedBatch batch);

	void SendCheckpoint(ulong targetSteamId);

	/// <summary>Host: the kernel run epoch this side is serving — the run identity a world-join instruction announces to the members.</summary>
	ulong CurrentRunEpoch { get; }

	/// <summary>
	/// Guest: the host announced the run identity this member must serve (the
	/// enter-the-world instruction). Every later checkpoint chunk set is
	/// validated against it before a chunk is buffered: a set from another run
	/// is refused, and the partial set already buffered is dropped the moment a
	/// different identity's chunk arrives. Zero means "no announcement" — the
	/// receiver keeps the identity it holds.
	/// </summary>
	void AdoptHostRunEpoch(ulong runEpoch);

	event Action<IReadOnlyList<WireItemMoveEntry>>? ItemMovesReceived;

	event Action<WirePayloadType, WireStateStream>? ItemStateStreamReceived;

	event Action<ulong, WirePayloadType, WireStateStream>? EntityStateStreamReceived;

	event Action<ulong, RejectionReason>? CommandRejected;

	void ResetForSessionEnd();
}
