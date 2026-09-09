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

	/// <summary>
	/// The presentation tint of a runtime-created crystalenemy (the same fact
	/// the live EntitySpawned channel carries in <see cref="EntitySpawnedMsg"/>,
	/// mirrored here for the late-joiner backfill — a fresh member materializes
	/// the copy WITHOUT the trigger-side SetColor, so the entry must carry the
	/// exact host-captured color). True = the carried color is meaningful (only
	/// crystalenemy entries set it; a false/zero entry is wire-identical to the
	/// old layout for every other prefab).
	/// </summary>
	[ProtoMember(5)]
	public bool HasTint { get; set; }

	/// <summary>The exact host-captured post-SetColor RGBA (only meaningful when <see cref="HasTint"/> is true).</summary>
	[ProtoMember(6)]
	public NetColorRgbaMsg TintColor { get; set; } = new();

	/// <summary>The exact host-captured light intensity (CrystalEnemy.cs:215 — only meaningful when <see cref="HasTint"/> is true).</summary>
	[ProtoMember(7)]
	public float LightIntensity { get; set; }

	/// <summary>
	/// The runtime-creation identity this animal was created by (sync-coverage
	/// audit E3's <see cref="RuntimeEntityKey"/>), when the host knows it. The
	/// late-joiner materialization stamps it onto the backfill copy so the copy
	/// carries the SAME identity as the live <c>EntitySpawned</c> copy: a
	/// surviving live re-report then binds it by key instead of duplicating the
	/// animal (the positional 1 m fallback is gone). Null for a runtime-spawn
	/// fact the host cannot attribute (an animal that never rode the creation
	/// channel — e.g. a host-mod spawn that bypassed it).
	/// </summary>
	[ProtoMember(8)]
	public RuntimeEntityKeyMsg? CreationKey { get; set; }
}
