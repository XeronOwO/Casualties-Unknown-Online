using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// The host's authoritative enemy ROSTER — the complete set of animal entities
/// with their presentation state. Sent to a member on its world entry (late
/// joiner / reconnect) AND on the host's 60 s in-session repair cycle (a member
/// that stays in the world otherwise had no second chance — audit row N1).
/// <para>
/// It is absolute and idempotent by ROW, not by a positional key: every entry
/// carries the host id that is the enemy's identity, and the member adopts its
/// local copies against the rows (reuse / materialize / destroy). An entry
/// that no longer appears is an enemy the member must stop showing; an EMPTY
/// list is the authoritative "this layer has no enemies" and the member
/// destroys its surplus copies rather than keeping a divergent local set.
/// </para>
/// <para>
/// <see cref="Generation"/> is the world/layer generation the roster was
/// derived in; the member refuses a stale one before applying anything. The
/// runtime-spawn facts ride beside the rows: they carry the creation key and
/// presentation extras a member needs to materialize a runtime row, looked up
/// by id.
/// </para>
/// </summary>
[ProtoContract]
public sealed class EnemySnapshotMsg
{
	[ProtoMember(1)]
	public List<EnemyStateMsg> Enemies { get; set; } = [];

	/// <summary>
	/// Runtime-created enemies only: the spawn facts (id + prefab + creation key
	/// + presentation extras) a member needs to materialize or bind a runtime
	/// row. Generation-time rows never appear here; their prefab comes from the
	/// kernel family and their binding is the row adoption.
	/// </summary>
	[ProtoMember(2)]
	public List<EnemySpawnEntryMsg> RuntimeSpawns { get; set; } = [];

	/// <summary>The world/layer generation this roster was derived in — a member refuses a roster of another generation (layers are regenerated, and the same ids never come back).</summary>
	[ProtoMember(3)]
	public WorldGenerationMsg? Generation { get; set; }
}
