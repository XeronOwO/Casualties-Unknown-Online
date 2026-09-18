using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// The host's authoritative enemy snapshot — the full set of animal entities
/// with their presentation state. Sent to a member on its world entry (late
/// joiner / reconnect) AND on the host's 60 s in-session repair cycle (a member
/// that stays in the world otherwise had no second chance — audit row N1). It is
/// absolute and idempotent: the guest pairs its locally generated copies on each
/// entry's <see cref="EnemyStateMsg.SpawnPosition"/> anchor, never on the live
/// position (which has moved on by the time a repair lands), and RuntimeSpawns
/// carries the runtime-spawn facts a member must materialize.
/// </summary>
[ProtoContract]
public sealed class EnemySnapshotMsg
{
	[ProtoMember(1)]
	public List<EnemyStateMsg> Enemies { get; set; } = [];

	/// <summary>
	/// Runtime-created enemies only: the spawn facts (id + prefab + current
	/// position/rotation) the member needs to materialize or bind the runtime
	/// copies it could not have generated. Generation-time enemies pair on the
	/// spawn anchor and never appear here. Empty = none.
	/// </summary>
	[ProtoMember(2)]
	public List<EnemySpawnEntryMsg> RuntimeSpawns { get; set; } = [];
}
