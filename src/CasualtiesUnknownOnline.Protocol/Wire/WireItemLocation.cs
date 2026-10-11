using ProtoBuf;

namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// Wire form of the kernel's single authoritative item location.
/// </summary>
[ProtoContract]
public sealed class WireItemLocation
{
	[ProtoMember(1)]
	public WireItemLocationKind Kind { get; set; }

	[ProtoMember(2)]
	public ulong Owner { get; set; }

	[ProtoMember(3)]
	public ulong ParentItemId { get; set; }

	[ProtoMember(4)]
	public float X { get; set; }

	[ProtoMember(5)]
	public float Y { get; set; }

	/// <summary>
	/// The limb an <see cref="WireItemLocationKind.Applied"/> item is installed
	/// on (an index into the body's limb array, the same index
	/// <c>CharacterLimbMsg.Index</c> carries) — absent for every other kind, which
	/// is why it is nullable: 0 is a real limb index (the head), so a
	/// non-nullable field could not tell "limb 0" from "no limb recorded", and an
	/// applied row that omits it is refused rather than read as the head.
	/// </summary>
	[ProtoMember(6)]
	public int? LimbIndex { get; set; }
}
