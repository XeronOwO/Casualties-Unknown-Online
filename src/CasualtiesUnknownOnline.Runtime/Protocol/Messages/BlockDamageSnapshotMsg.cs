using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// The partial block-damage ROW SET, in two directions with two meanings:
/// <list type="bullet">
/// <item>Host → member: every partially-damaged block this host holds (world
/// entry / the 60 s resend, sent alongside the block-state, trap-state,
/// opened-entities and building-entity-health snapshots), and — as the ANSWER to
/// one report — this host's authoritative value for exactly the cells that
/// report named (<see cref="AnswersReport"/>). Block-cell-keyed: the receiver
/// finds its own deterministically-generated copy at each cell and writes the
/// host's accumulated damage ABSOLUTELY.</item>
/// <item>Guest → host (<see cref="NetMsg.BlockDamageReport"/>): the cells whose
/// live delta report this host never accounted for, each carrying the damage
/// THIS SENDER has cumulatively applied to it — its own contribution, never the
/// cell's total (see <see cref="BlockDamagedMsg.Contribution"/>). Rows the host's
/// own rules refuse come back answered as 0, which clears the reporter's
/// outstanding entry instead of letting it re-report forever.</item>
/// </list>
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

	/// <summary>
	/// Host → guest (protocol 32): this set is the ANSWER to a report from the
	/// receiver, so the cells it names are accounted for — the receiver clears
	/// those cells' outstanding entries (keeping its cumulative contribution,
	/// which the ledger on the other side now holds). False for the periodic /
	/// world-entry snapshot, which is authoritative STATE and says nothing about
	/// whether a particular sender's outstanding report was accounted for: a
	/// third party's entry must survive it, or its contribution would never be
	/// reported at all (the under-count this protocol closes).
	/// </summary>
	[ProtoMember(3)]
	public bool AnswersReport { get; set; }
}
