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

	/// <summary>
	/// The world/layer generation these rows describe (protocol 29) — the same
	/// stamp the live block-damage family carries, because the rows are keyed by
	/// block cell: a snapshot or a guest's absolute re-report that crossed a
	/// generation boundary would write another world's damage into this one.
	/// Null = the sender has no committed run baseline yet (UNKNOWN).
	/// </summary>
	[ProtoMember(2)]
	public WorldGenerationMsg? Generation { get; set; }
}
