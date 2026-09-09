using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// A world entity was created at RUNTIME (outside world generation — the spawn
/// command, a scripted create): bidirectional, BlockPlaced semantics — the
/// creating side keeps its local copy, reports; the host creates its own copy
/// at the same position and relays (the source excluded); every receiving side
/// creates the same entity. World generation is deterministic on both sides,
/// so the position-keyed identity holds for RUNTIME creations too once both
/// sides hold the entity. Items do NOT ride this channel — the item domain
/// (ItemInstanceId + ItemSpawn) already syncs runtime item creation.
/// </summary>
[ProtoContract]
public sealed class EntitySpawnedMsg
{
	/// <summary>The entity's prefab id (BuildingEntity.id / Utils.Create's first argument, e.g. "landmine").</summary>
	[ProtoMember(1)]
	public string Id { get; set; } = string.Empty;

	/// <summary>The entity's world position.</summary>
	[ProtoMember(2)]
	public NetVector2Msg Position { get; set; } = new();

	/// <summary>The entity's z rotation (euler).</summary>
	[ProtoMember(3)]
	public float Rotation { get; set; }

	/// <summary>
	/// Creation-time initial data (#128): a geyser's liquid type (1/2 — never 0,
	/// so the protobuf zero-omission cannot bite). The type is rolled at the
	/// entity's own Start (GeyserScript.cs:12) from the per-side random stream,
	/// so the creating side reads its copy one frame later and the receiving
	/// sides apply the carried value AFTER their own copy's Start re-rolled it
	/// — the creation carries its initial condition, one message per operation.
	/// 0 = no extra data (non-geyser entities).
	/// </summary>
	[ProtoMember(4)]
	public byte LiquidType { get; set; }

	/// <summary>
	/// Creation-time initial data (#128): a runtime-created keypad's code. The
	/// game lazy-generates the code on FIRST USE per side (Openable.cs:19) —
	/// every side would get its own. The HOST generates it at relay time (its
	/// Random stream decides — same authority as the generation-time keypad
	/// snapshot) and the receiving sides write it onto their copy; the lazy
	/// generation skips an already-set code, so no Start wait is needed here.
	/// Empty = no code (non-keypad entities).
	/// </summary>
	[ProtoMember(5)]
	public string KeypadCode { get; set; } = string.Empty;

	/// <summary>
	/// Creation-time initial data: the presentation tint of a runtime-created
	/// crystalenemy (CrystalMimic.cs:32/46 — the mimic writes
	/// <c>CrystalEnemy.SetColor(crystal.sprite.color)</c> on the triggering side
	/// ONLY; the jitter that SetColor applies comes from the PER-SIDE random
	/// stream, so the copy must be written the EXACT captured color, never a
	/// re-roll). The host reads its copy's post-SetColor <c>sprite.color</c> /
	/// <c>light intensity</c> at the spawn report (BuildingEntity.Start runs one
	/// frame after the same-frame SetColor) and carries them; every receiving
	/// side writes them onto its created copy directly. True = the carried color
	/// is meaningful (only crystalenemy creations set it — the protobuf
	/// zero-omission cannot bite because an explicit flag rides the payload).
	/// </summary>
	[ProtoMember(6)]
	public bool HasEnemyTint { get; set; }

	/// <summary>The exact post-SetColor RGBA (only meaningful when <see cref="HasEnemyTint"/> is true).</summary>
	[ProtoMember(7)]
	public NetColorRgbaMsg EnemyTintColor { get; set; } = new();

	/// <summary>The exact post-SetColor light intensity (CrystalEnemy.cs:215 — Random.Range(0.5, 1), only meaningful when <see cref="HasEnemyTint"/> is true).</summary>
	[ProtoMember(8)]
	public float EnemyLightIntensity { get; set; }

	/// <summary>
	/// True when the created entity is an ANIMAL (BuildingEntity.animal). The
	/// live creation message is unchanged, but the world-entity recovery treats
	/// it specially: the HOST's accepted-creation table must not record it
	/// (the enemy domain's <c>EnemySnapshot.RuntimeSpawns</c> owns the
	/// late-join/backfill copy and materializes at the animal's CURRENT position
	/// with a host-allocated id — a host record would make a late joiner create
	/// a second copy at the creation position). The GUEST pending table still
	/// records it, because a swallowed guest → host animal report has no other
	/// in-session recovery; that entry is dropped by the host's relay echo, by
	/// the absolute snapshot's animal key list, or by the entity's death
	/// (reported with its stamped creation key, so a moved animal cannot leave
	/// it behind).
	/// </summary>
	[ProtoMember(9)]
	public bool IsAnimal { get; set; }

	/// <summary>
	/// The creation-instance token, half 1: the SteamId of the side that created
	/// the entity. The recovery key is prefab id + creation cell + this token,
	/// because two identical prefabs created in the SAME cell (1.0-1.4 m apart)
	/// are distinct creations — the cell alone cannot tell them apart, and a
	/// second report used to bind to (and overwrite) the first copy. 0 = no
	/// token (a hand-built message; the adapter always stamps one).
	/// </summary>
	[ProtoMember(10)]
	public ulong CreatorSteamId { get; set; }

	/// <summary>
	/// The creation-instance token, half 2: a monotonic per-creator sequence,
	/// stamped by the creating side at report time and carried unchanged through
	/// the relay, the snapshot and every re-report. 0 = no token; a real token
	/// starts at 1, so the protobuf zero-omission cannot bite.
	/// </summary>
	[ProtoMember(11)]
	public uint CreationSequence { get; set; }

	/// <summary>True when the message carries a creation-instance token (the creating side stamped it).</summary>
	public bool HasCreationToken => CreationSequence != 0;
}
