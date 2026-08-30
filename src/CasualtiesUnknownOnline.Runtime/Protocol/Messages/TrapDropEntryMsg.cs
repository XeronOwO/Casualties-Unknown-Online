using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// One item a destructive trap/building entity dropped on the triggering side,
/// carried inside <see cref="EntityEventMsg.Drops"/> so the trap trigger and
/// its item spawns are committed as one atomic kernel composite on the host.
/// Full initial state (same shape as a block-break drop report) so a receiver
/// can materialize the drop exactly.
/// </summary>
[ProtoContract]
public sealed class TrapDropEntryMsg
{
	[ProtoMember(1)]
	public ulong ItemId { get; set; } // instance id: (triggering side SteamId, local counter)

	[ProtoMember(2)]
	public CharacterItemMsg Item { get; set; } = new();

	[ProtoMember(3)]
	public NetVector2Msg Position { get; set; } = new();

	[ProtoMember(4)]
	public NetVector2Msg Velocity { get; set; } = new();

	[ProtoMember(5)]
	public float Rotation { get; set; } // z euler angle

	[ProtoMember(6)]
	public bool FreshItemDrop { get; set; }

	[ProtoMember(7)]
	public float AngularVelocity { get; set; }
}
