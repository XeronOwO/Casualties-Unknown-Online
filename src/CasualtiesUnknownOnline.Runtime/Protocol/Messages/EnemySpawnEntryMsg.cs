using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// One runtime-created enemy's spawn fact (host → guest, carried by the
/// world-entry <see cref="EnemySnapshotMsg"/>): the enemy id the host assigned,
/// the prefab to instantiate on the receiving side and the authoritative
/// current position/rotation. Generation-time enemies never appear here — the
/// receiving side already generated them deterministically. The live creation
/// command still rides the EntitySpawned channel; this entry is the late-joiner
/// backfill that materializes the runtime copies a fresh member could not have
/// generated.
/// </summary>
[ProtoContract]
public sealed class EnemySpawnEntryMsg
{
	[ProtoMember(1)]
	public NetworkEntityIdMsg Id { get; set; } = new();

	/// <summary>The prefab id (BuildingEntity.id, e.g. "cavetick") to instantiate when no local copy exists.</summary>
	[ProtoMember(2)]
	public string PrefabId { get; set; } = string.Empty;

	/// <summary>The authoritative current world position (the snapshot state lands on top of this copy).</summary>
	[ProtoMember(3)]
	public NetVector2Msg Position { get; set; } = new();

	/// <summary>The authoritative current z rotation (euler degrees).</summary>
	[ProtoMember(4)]
	public float Rotation { get; set; }
}
