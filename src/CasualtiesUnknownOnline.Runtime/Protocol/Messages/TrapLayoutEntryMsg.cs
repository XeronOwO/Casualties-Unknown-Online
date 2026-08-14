using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// One trap/mechanism entity in the host's generated layout — the position-key
/// identity (the world entities' generation-time identity, same as the trap
/// consumption registry) plus the prefab name the host instantiated it from
/// (the guest materializes a missing copy from it — never a hand-built
/// kind→prefab table, the host's own scene IS the fact).
/// </summary>
[ProtoContract]
public sealed class TrapLayoutEntryMsg
{
	[ProtoMember(1)]
	public EntityEventKind Kind { get; set; }

	[ProtoMember(2)]
	public float X { get; set; }

	[ProtoMember(3)]
	public float Y { get; set; }

	[ProtoMember(4)]
	public string PrefabName { get; set; } = "";
}
