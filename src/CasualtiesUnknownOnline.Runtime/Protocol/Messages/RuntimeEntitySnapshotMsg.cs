using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// The host's absolute runtime-created world-entity table (sync-coverage audit
/// E3): every creation the host accepted — its own and the guests' reports —
/// as the exact <see cref="EntitySpawnedMsg"/> the live channel relays (prefab
/// id, position, rotation and the creation-time payload: keypad code, geyser
/// liquid type, crystal tint). Sent on world entry inside the ordered snapshot
/// group and re-broadcast by the 60 s host cycle, so a swallowed creation
/// report or relay heals without a reconnect.
/// <para>
/// Apply is ADDITIVE and idempotent: each entry runs the live creation path
/// (<c>EntitySpawnSync.FindExisting</c>'s ~1 m same-id dedup), so an entity the
/// receiver already has is left alone and the wire is safe to repeat. It is
/// deliberately not a destructive alignment (unlike the trap layout): a local
/// creation whose report the host has not answered yet must never be destroyed
/// by a snapshot that predates it.
/// </para>
/// </summary>
[ProtoContract]
public sealed class RuntimeEntitySnapshotMsg
{
	[ProtoMember(1)]
	public List<EntitySpawnedMsg> Entries { get; set; } = [];
}
