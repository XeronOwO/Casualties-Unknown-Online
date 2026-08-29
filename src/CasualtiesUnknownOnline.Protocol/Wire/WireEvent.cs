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
}
