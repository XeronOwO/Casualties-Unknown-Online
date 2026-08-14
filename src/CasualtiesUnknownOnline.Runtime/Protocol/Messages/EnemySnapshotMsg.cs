using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// The host's authoritative enemy snapshot — the full set of animal entities
/// with their presentation state. Sent to a member on its world entry (late
/// joiner / reconnect) so it binds its locally generated enemy copies to the
/// host's ids, and carried by the periodic enemy-state broadcast.
/// </summary>
[ProtoContract]
public sealed class EnemySnapshotMsg
{
	[ProtoMember(1)]
	public List<EnemyStateMsg> Enemies { get; set; } = [];
}
