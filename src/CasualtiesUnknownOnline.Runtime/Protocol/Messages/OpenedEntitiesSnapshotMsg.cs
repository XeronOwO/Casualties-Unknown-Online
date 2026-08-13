using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Host → member: the opened lockable entities so far (world entry, sent
/// alongside the block-state and trap-state snapshots). Position-keyed — the
/// receiver finds its own deterministically-generated copy at each position
/// and applies the open (health = 0), the same application as the live
/// BuildingEntityOpened relay.
/// </summary>
[ProtoContract]
public sealed class OpenedEntitiesSnapshotMsg
{
	[ProtoMember(1)]
	public List<NetVector2Msg> Positions { get; set; } = [];
}
