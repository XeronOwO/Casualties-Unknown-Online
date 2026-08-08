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
}
