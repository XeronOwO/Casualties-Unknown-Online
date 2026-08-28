using System;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// The carried-item fact event aggregator: one carried item's authoritative
/// state changed — a use flipped its component state, a slot move re-homed it,
/// a pickup brought it into the inventory. The new protocol carries these facts
/// inside <c>CommittedBatchEnvelope</c> projections; this service only owns the
/// local event fan-out and the host-local apply step. No wire state remains.
/// </summary>
public sealed class ItemCarriedSyncService
{
	/// <summary>The authoritative fact of one carried item changed (host broadcast: use/slot move/pickup) — the adapter updates the owner's per-player fact table and re-renders the clone. Fired on the guests from the wire and on the host directly (its own arbitration decisions).</summary>
	public event Action<ulong, CharacterItemMsg, bool>? ItemCarriedSyncReceived;

	public void FireItemCarriedSyncReceived(ulong sender, ulong ownerSteamId, CharacterItemMsg item, bool slotKnown)
		=> ItemCarriedSyncReceived?.Invoke(ownerSteamId, item, slotKnown);

	/// <summary>Apply a carried fact locally only (no broadcast — the caller's own relay already carries it to the peers; one operation = one message): this side's clone of the owner re-renders.</summary>
	public void PublishLocal(ulong ownerSteamId, CharacterItemMsg item)
		=> ItemCarriedSyncReceived?.Invoke(ownerSteamId, item, item.SlotIndex != -1);
}
