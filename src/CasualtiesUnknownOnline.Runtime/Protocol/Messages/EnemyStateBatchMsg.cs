using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Host → guest: the authoritative enemy-state batch (20 Hz). Sent unreliably
/// with overwrite semantics (newest wins) — drops are fine and the sequence
/// number lets the receiver discard stale snapshots (the unreliable channel
/// does not guarantee order). Mirrors <see cref="PlayerStateMsg"/>.
/// </summary>
[ProtoContract]
public sealed class EnemyStateBatchMsg
{
	[ProtoMember(1)]
	public List<EnemyStateMsg> Enemies { get; set; } = [];

	[ProtoMember(2)]
	public uint Seq { get; set; }
}
