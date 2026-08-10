using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>One world item in <see cref="ItemSnapshotMsg"/>.</summary>
[ProtoContract]
public sealed class ItemSnapshotEntryMsg
{
	[ProtoMember(1)]
	public ulong ItemId { get; set; }

	[ProtoMember(2)]
	public CharacterItemMsg Item { get; set; } = new();

	[ProtoMember(3)]
	public NetVector2Msg Position { get; set; } = new();

	[ProtoMember(4)]
	public NetVector2Msg Velocity { get; set; } = new();

	[ProtoMember(5)]
	public ulong ParentItemId { get; set; } // 0 = the world, else the containing world container item's instance id

	[ProtoMember(6)]
	public float Rotation { get; set; } // z euler angle

	[ProtoMember(7)]
	public bool FreshItemDrop { get; set; } // the glowing floating pickup effect

	[ProtoMember(8)]
	public NetVector2Msg? ParentPosition { get; set; } // the container's world position when ParentItemId is set (binding by position for generation-time containers)

	[ProtoMember(9)]
	public float AngularVelocity { get; set; } // the item's spin (part of the initial condition for a rolled item)

	/// <summary>Carried-entry marker (generation snapshot only). Wire encoding:
	/// slotIndex + 1, 0 = a world item (NOT the raw index — protobuf-net omits
	/// 0-valued ints, and a starting supply in backpack slot 0 has raw index 0:
	/// a raw-encoded slot-0 item would arrive as a world item).</summary>
	[ProtoMember(10)]
	public int SlotIndex { get; set; }

}
