using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// One consumed/changed item in a CraftReportMsg. The PostState digest is the
/// post-operation state (for a Destroyed entry it is an id-only stub — the
/// item no longer exists to capture). The InstanceId lives in the digest
/// (CharacterItemMsg.InstanceId) — one id source, no entry-level duplicate.
/// </summary>
[ProtoContract]
public sealed class CraftEntryMsg
{
	[ProtoMember(1)]
	public CraftEntryDisposition Disposition { get; set; } // Destroyed = 0 — the wire default, omission transparent

	[ProtoMember(2)]
	public CharacterItemMsg Item { get; set; } = new(); // post-operation digest (id-only stub for Destroyed)

	/// <summary>Host-stamped apply routing for the relay's receivers (the report side leaves it None — the host knows the table membership, the guests' tables are empty). None = 0 — the wire default, omission transparent.</summary>
	[ProtoMember(3)]
	public CraftApplyKind ApplyKind { get; set; } // WorldCorrection = the Changed entry is a world item — the receivers correct their scene copy
}
