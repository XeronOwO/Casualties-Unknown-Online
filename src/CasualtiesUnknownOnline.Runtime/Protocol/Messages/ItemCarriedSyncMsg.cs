using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// The authoritative state of one CARRIED item (in a backpack, a hand slot or
/// worn on a limb): the host broadcasts it the moment the item's fact changed —
/// a use flipped its component state, a slot move re-homed it, a pickup brought
/// it into the inventory. The guests apply it to their per-player character
/// fact table (_cloneData) and re-render that player's clone immediately; the
/// 1 Hz character snapshot stays as the fallback. The item LEAVING an
/// inventory travels the existing ItemDrop report instead (the world
/// materializes it there). SlotKnown = false means the carried slot is
/// meaningless (SlotIndex -1 — not in any slot or limb: a use whose slot could
/// not be resolved, a world entry) — the receiver keeps the fact table's
/// existing slot for the item; slot indices 0..n and the limb wear encodings
/// (≤ -2) are always valid.
/// </summary>
[ProtoContract]
public sealed class ItemCarriedSyncMsg
{
	/// <summary>The item's owner (SteamId) — the fact table and the render are keyed per player.</summary>
	[ProtoMember(1)]
	public ulong OwnerSteamId { get; set; }

	/// <summary>The item's full authoritative state (condition/components/liquids/contents — the snapshot capture shape, never the digest).</summary>
	[ProtoMember(2)]
	public CharacterItemMsg Item { get; set; } = new();

	/// <summary>False = the item's slot is meaningless (-1) — keep the fact table's existing slot; true = 0..n or a limb wear encoding (≤ -2).</summary>
	[ProtoMember(3)]
	public bool SlotKnown { get; set; }
}
