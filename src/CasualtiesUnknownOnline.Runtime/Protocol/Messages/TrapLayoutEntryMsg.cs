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

	/// <summary>
	/// The runtime-creation identity of the entity behind this entry, when the
	/// host's own copy carries a <c>RuntimeEntityCreation</c> marker (a
	/// runtime-created trap/mechanism): the guest's materialized copy is stamped
	/// with the same key, so the runtime-entity snapshot binds it by identity
	/// instead of by proximity (the positional 1 m fallback is gone). Null for a
	/// generation-time entity — it has no creation record and no marker.
	/// </summary>
	[ProtoMember(5)]
	public RuntimeEntityKeyMsg? CreationKey { get; set; }
}
