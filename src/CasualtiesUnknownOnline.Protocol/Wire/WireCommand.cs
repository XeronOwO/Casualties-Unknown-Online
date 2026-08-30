using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// Typed wire command payload. One command is one logical operation; the
/// kernel maps this to a typed GameState command on the host.
/// </summary>
[ProtoContract]
public sealed class WireCommand
{
	[ProtoMember(1)]
	public WireCommandKind Kind { get; set; }

	[ProtoMember(2)]
	public WireItemIdentity Identity { get; set; } = new();

	[ProtoMember(3)]
	public WireItemLocation? Location { get; set; }

	[ProtoMember(4)]
	public WireItemData? Data { get; set; }

	[ProtoMember(5)]
	public ulong ExpectedRevision { get; set; }

	[ProtoMember(6)]
	public ulong NewOwner { get; set; }

	[ProtoMember(7)]
	public WireTerminalKind TerminalKind { get; set; }

	[ProtoMember(8)]
	public ulong RangeStart { get; set; }

	[ProtoMember(9)]
	public ulong RangeEnd { get; set; }

	[ProtoMember(10)]
	public List<WireContainerChild> ContainerChildren { get; set; } = [];

	[ProtoMember(11)]
	public int RejectionReason { get; set; }

	[ProtoMember(12)]
	public WireRunState? RunState { get; set; }

	[ProtoMember(13)]
	public WireEntityPosition? EntityPosition { get; set; }

	[ProtoMember(14)]
	public int EntityKind { get; set; }

	[ProtoMember(15)]
	public byte Extra { get; set; }

	[ProtoMember(16)]
	public float Health { get; set; }

	[ProtoMember(17)]
	public long TriggeredAtMs { get; set; }

	[ProtoMember(18)]
	public WirePlayerState? PlayerState { get; set; }

	[ProtoMember(19)]
	public WireEntityId? EntityId { get; set; }

	[ProtoMember(20)]
	public WireEnemyState? EnemyState { get; set; }

	[ProtoMember(21)]
	public WireFluidRegionState? FluidState { get; set; }

	[ProtoMember(22)]
	public ulong CarrierSteamId { get; set; }

	[ProtoMember(23)]
	public ulong CarriedSteamId { get; set; }

	[ProtoMember(24)]
	public WireEnemyCombat? EnemyCombat { get; set; }
}
