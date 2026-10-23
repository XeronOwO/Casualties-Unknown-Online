using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Receiving client → host: the OUTCOME of one give's placement, reported by the client that ran it. The host
/// recorded the place the answered destination named; this message states the place the placement actually used,
/// so the host compares the claim with a fact it already holds instead of taking the receiver's word.
/// <para>
/// A matching place settles the give and writes nothing. A different place — the receiving client's own rules
/// refused the answered container and it fell back, or its live body changed in the answer-to-transfer window —
/// is refused by name and the row goes back to the giver. So is the not-applied arm (<see cref="Applied"/>
/// false), which is the residual the bare failure report used to carry: the item goes to the giver exactly as the
/// user's 2026-10-22 ruling asks, but the report now has to name what it did, and a receiver that materialized the
/// item cannot claim it placed nothing without contradicting the place it would have named.
/// </para>
/// <para>
/// One outcome per give: the host takes its committed record on the report, so a repeated or late outcome cannot
/// return the same item twice. A report matching no committed give moves nothing at all.
/// </para>
/// </summary>
[ProtoContract]
public sealed class PlayerPlacementOutcomeMsg
{
	/// <summary>The given item's stable instance id — the host matches it against the placement it committed.</summary>
	[ProtoMember(1)]
	public ulong ItemInstanceId { get; set; }

	/// <summary>Whether this client placed the item at all. False is the residual: it applied nothing, so the host returns the row.</summary>
	[ProtoMember(2)]
	public bool Applied { get; set; }

	/// <summary>The body slot the item landed in, or -1 when it landed in a container (or nowhere).</summary>
	[ProtoMember(3)]
	public int Slot { get; set; } = -1;

	/// <summary>The container the item landed in, or 0 when it landed in a body slot (or nowhere).</summary>
	[ProtoMember(4)]
	public ulong ContainerInstanceId { get; set; }

	/// <summary>
	/// This client's own sentence: why it applied nothing, or why the place it used is not the one it answered.
	/// Capped and stripped of control characters before it is logged or relayed.
	/// </summary>
	[ProtoMember(5)]
	public string Reason { get; set; } = "";
}
