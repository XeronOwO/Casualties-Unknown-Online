using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Host → guest: the authoritative batch of entity states (20 Hz). Sent
/// unreliably — the stream is overwrite-semantics (newest wins), so drops are
/// fine and the sequence number lets the receiver discard stale snapshots
/// (the unreliable channel does not guarantee order).
/// </summary>
[ProtoContract]
public sealed class PlayerStateMsg
{
	[ProtoMember(1)]
	public List<EntityStateMsg> Entities { get; set; } = [];

	[ProtoMember(2)]
	public uint Seq { get; set; }
}
