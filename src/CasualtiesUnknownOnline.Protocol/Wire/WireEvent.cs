using ProtoBuf;

namespace CasualtiesUnknownOnline.Protocol.Wire;

/// <summary>
/// Typed wire form of one kernel domain event. The event name is a domain fact
/// (ItemSpawned, ItemRelocated, ...), not a Harmony hook name.
/// </summary>
[ProtoContract]
public sealed class WireEvent
{
	[ProtoMember(1)]
	public WireEventKind Kind { get; set; }

	[ProtoMember(2)]
	public WireItemIdentity Identity { get; set; } = new();

	[ProtoMember(3)]
	public ulong OldRevision { get; set; }

	[ProtoMember(4)]
	public ulong NewRevision { get; set; }

	[ProtoMember(5)]
	public WireItemLocation? OldLocation { get; set; }

	[ProtoMember(6)]
	public WireItemLocation? NewLocation { get; set; }

	[ProtoMember(7)]
	public WireItemData? OldData { get; set; }

	[ProtoMember(8)]
	public WireItemData? NewData { get; set; }

	[ProtoMember(9)]
	public WireTerminalKind TerminalKind { get; set; }

	[ProtoMember(10)]
	public WireRunState? RunState { get; set; }

	[ProtoMember(11)]
	public WireEntityPosition? EntityPosition { get; set; }

	[ProtoMember(12)]
	public int EntityKind { get; set; }

	[ProtoMember(13)]
	public byte Extra { get; set; }

	[ProtoMember(14)]
	public float Health { get; set; }

	[ProtoMember(15)]
	public long TriggeredAtMs { get; set; }

	[ProtoMember(16)]
	public WirePlayerState? PlayerState { get; set; }

	[ProtoMember(17)]
	public WireEntityId? EntityId { get; set; }

	[ProtoMember(18)]
	public WireEnemyState? EnemyState { get; set; }

	[ProtoMember(19)]
	public WireFluidRegionState? FluidState { get; set; }

	[ProtoMember(20)]
	public ulong CarrierSteamId { get; set; }

	[ProtoMember(21)]
	public ulong CarriedSteamId { get; set; }

	[ProtoMember(22)]
	public WirePlayerInteraction? PlayerInteraction { get; set; }
}
