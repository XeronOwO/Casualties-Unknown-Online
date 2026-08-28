using ProtoBuf;

namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// Wire form of one world-item snapshot entry: the full rebuildable item fact
/// plus the motion/container presentation fields required to materialize it.
/// Used by the Phase C state-stream fallback for periodic and generation-time
/// item snapshots.
/// </summary>
[ProtoContract]
public sealed class WireWorldItemState
{
	[ProtoMember(1)]
	public WireItemIdentity Identity { get; set; } = new();

	[ProtoMember(2)]
	public WireItemData Data { get; set; } = new();

	[ProtoMember(3)]
	public float X { get; set; }

	[ProtoMember(4)]
	public float Y { get; set; }

	[ProtoMember(5)]
	public float VelX { get; set; }

	[ProtoMember(6)]
	public float VelY { get; set; }

	[ProtoMember(7)]
	public ulong ParentItemId { get; set; }

	[ProtoMember(8)]
	public float Rotation { get; set; }

	[ProtoMember(9)]
	public bool FreshItemDrop { get; set; }

	[ProtoMember(10)]
	public float ParentX { get; set; }

	[ProtoMember(11)]
	public float ParentY { get; set; }

	[ProtoMember(12)]
	public float AngularVelocity { get; set; }

	[ProtoMember(13)]
	public int SlotIndex { get; set; }
}
