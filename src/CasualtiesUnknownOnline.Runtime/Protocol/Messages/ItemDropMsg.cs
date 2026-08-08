using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// An item left a player's inventory/container into the world (dropped,
/// thrown, container unloaded): guest → host as a report (the host registers
/// it in the world-item table and relays), host → guest as a broadcast relay
/// (the source excluded). Carries the full item state — the receivers without
/// the item (it was inside a remote inventory) materialize it at Position.
/// </summary>
[ProtoContract]
public sealed class ItemDropMsg
{
	[ProtoMember(1)]
	public ulong ItemId { get; set; }

	[ProtoMember(2)]
	public CharacterItemMsg Item { get; set; } = new();

	[ProtoMember(3)]
	public NetVector2Msg Position { get; set; } = new();

	[ProtoMember(4)]
	public ulong ParentItemId { get; set; } // 0 = the world, else the containing world container item's instance id

	[ProtoMember(5)]
	public float Rotation { get; set; } // z euler angle (the slot's rotation when dropped)

	[ProtoMember(6)]
	public NetVector2Msg? Velocity { get; set; } // the item's velocity at the drop moment (a throw carries a big one)
}
