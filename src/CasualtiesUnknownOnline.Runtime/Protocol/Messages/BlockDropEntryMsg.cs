using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// One item a broken block dropped on the breaking side — carried inside
/// BlockDamagedMsg.Drops so the break and its drops get ONE arbitration verdict
/// (first-writer-wins: the accepted report's drops register and materialize
/// everywhere, the rejected report's drops are rolled back on the breaker).
/// Full initial state (same shape as the world-item report) so a receiver can
/// materialize the drop exactly — the break's drops are the local compute of
/// the breaker, the peers only render them.
/// </summary>
[ProtoContract]
public sealed class BlockDropEntryMsg
{
	[ProtoMember(1)]
	public ulong ItemId { get; set; } // instance id: (breaker SteamId, local counter)

	[ProtoMember(2)]
	public CharacterItemMsg Item { get; set; } = new();

	[ProtoMember(3)]
	public NetVector2Msg Position { get; set; } = new();

	[ProtoMember(4)]
	public NetVector2Msg Velocity { get; set; } = new();

	[ProtoMember(5)]
	public float Rotation { get; set; } // z euler angle — drops spawn with random rotations

	[ProtoMember(6)]
	public bool FreshItemDrop { get; set; } // the glowing floating pickup effect (FreshItemDrop.cs)

	[ProtoMember(7)]
	public float AngularVelocity { get; set; } // the drop's spin at the spawn moment (a rolling drop's initial condition)
}
