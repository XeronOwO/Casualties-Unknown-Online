using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Host → guest: the one-shot trap consumptions so far (mine exploded,
/// spikestabber activated, stalactite dropped, ...). Sent on world entry
/// alongside the block-state snapshot — the late joiner consumes each entry
/// against its own deterministic world (idempotent: an already-destroyed
/// entity is skipped). Repeatable traps (clamps, fences, coils, geysers,
/// jump pads, ...) are NOT recorded — each side's copy re-arms naturally,
/// which is the vanilla behaviour.
/// </summary>
[ProtoContract]
public sealed class TrapStateSnapshotMsg
{
	[ProtoMember(1)]
	public List<EntityEventMsg> Consumed { get; set; } = [];
}
