using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// The full authoritative world-item table, sent to a member on its world
/// entry (late joiner / reconnect) so it sees every runtime-generated world
/// item. The receiver reconciles: spawns the missing, destroys the stale,
/// moves the moved.
/// </summary>
[ProtoContract]
public sealed class ItemSnapshotMsg
{
	[ProtoMember(1)]
	public List<ItemSnapshotEntryMsg> Entries { get; set; } = [];
}
