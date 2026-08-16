using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// One partially-damaged block in the host's authoritative block-damage table.
/// Block-space integer coordinates — the cell IS the identity (both sides
/// generate the same world from the same RNG baseline); Damage is the host's
/// current accumulated <c>BlockDamage.damage</c> for a block that has NOT
/// broken yet (a broken block rides the block-state snapshot instead).
/// </summary>
[ProtoContract]
public sealed class BlockDamageEntryMsg
{
	[ProtoMember(1)]
	public int X { get; set; }

	[ProtoMember(2)]
	public int Y { get; set; }

	[ProtoMember(3)]
	public float Damage { get; set; }
}
