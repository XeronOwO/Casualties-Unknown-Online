using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// The host's authoritative trap layout (world entry, sent alongside the
/// block-state / trap-state / opened-entities snapshots): the generated
/// trap/mechanism entities' positions. The game distributes entities with
/// PHYSICS queries (DistributeEntities' OverlapPoint/Raycast and PlaceBody's
/// collider scan — WorldGeneration.cs) that are NOT covered by the
/// random-stream isolation, so the two sides' entity layouts diverge while
/// the block fingerprint stays identical (observed: the host's spike at
/// (-13,466.8), the guest's nearest 42 units away — the guest regenerated a
/// different layout). The guest aligns its world to this list: missing
/// entries materialize (prefab name), surplus/off-position entities are
/// destroyed — the host's scene is the authority.
/// </summary>
[ProtoContract]
public sealed class TrapLayoutSnapshotMsg
{
	[ProtoMember(1)]
	public List<TrapLayoutEntryMsg> Entries { get; set; } = [];
}
