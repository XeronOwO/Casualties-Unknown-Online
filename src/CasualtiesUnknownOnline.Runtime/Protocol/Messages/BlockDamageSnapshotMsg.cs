using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Host → member: every partially-damaged block so far (world entry / the
/// 60 s resend, sent alongside the block-state, trap-state, opened-entities
/// and building-entity-health snapshots). Block-cell-keyed — the receiver
/// finds its own deterministically-generated copy at each cell and writes the
/// host's accumulated damage ABSOLUTELY (the same semantic as the live
/// BlockDamaged relay, but for damage that accumulated before the member
/// joined).
/// </summary>
[ProtoContract]
public sealed class BlockDamageSnapshotMsg
{
	[ProtoMember(1)]
	public List<BlockDamageEntryMsg> Entries { get; set; } = [];
}
