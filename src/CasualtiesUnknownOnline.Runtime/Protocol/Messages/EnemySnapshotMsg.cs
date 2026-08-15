using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// The host's authoritative enemy snapshot — the full set of animal entities
/// with their presentation state. Sent to a member on its world entry (late
/// joiner / reconnect) so it binds its locally generated enemy copies to the
/// host's ids; RuntimeSpawns carries the runtime-spawn facts a late joiner must materialize.
/// </summary>
[ProtoContract]
public sealed class EnemySnapshotMsg
{
	[ProtoMember(1)]
	public List<EnemyStateMsg> Enemies { get; set; } = [];

	/// <summary>
	/// Runtime-created enemies only: the spawn facts (id + prefab + current
	/// position/rotation) the member needs to materialize or bind the runtime
	/// copies it could not have generated. Generation-time enemies pair by the
	/// deterministic spawn position and never appear here. Empty = none.
	/// </summary>
	[ProtoMember(2)]
	public List<EnemySpawnEntryMsg> RuntimeSpawns { get; set; } = [];
}
