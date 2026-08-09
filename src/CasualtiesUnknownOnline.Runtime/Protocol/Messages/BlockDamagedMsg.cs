using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Either side → peer: a block was damaged at a world position (local compute,
/// remote verify/sync). Carries the block's drops when the damage BROKE the
/// block — the break and its drops are ONE message, so the host's arbitration
/// gives them one verdict: first-writer-wins (the accepted report's drops
/// register and materialize everywhere; the rejected report's drops are rolled
/// back on the breaker via ItemReject).
/// </summary>
[ProtoContract]
public sealed class BlockDamagedMsg
{
	[ProtoMember(1)]
	public NetVector2Msg Position { get; set; } = new();

	[ProtoMember(2)]
	public float Damage { get; set; }

	/// <summary>The drops the BREAK created on the damaging side (null/empty = damage only — the block survived).</summary>
	[ProtoMember(3)]
	public List<BlockDropEntryMsg>? Drops { get; set; }
}
