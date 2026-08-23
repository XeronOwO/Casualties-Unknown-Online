using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// A player → host request to recruit a dead in-world teammate at a trader
/// (KrokMP-inspired co-op mechanic). The acting side computed the nearest
/// trader locally; the host is the authority for the trade gates and the
/// revive result. The request is deliberately a dedicated message (not a
/// TraderActionKind) because there is no vanilla game method to run local-only:
/// the only local effect on the acting side is the UI click, and the entire
/// outcome is host-decided.
/// </summary>
[ProtoContract]
public sealed class TraderRecruitRequestMsg
{
	/// <summary>The dead player the recruiter wants revived.</summary>
	[ProtoMember(1)]
	public ulong TargetSteamId { get; set; }

	/// <summary>The trader's world position the acting side clicked near — the
	/// host locates its own trader by the same position key used by the trade
	/// domain (both sides generated the same traders at the same positions).</summary>
	[ProtoMember(2)]
	public NetVector2Msg TraderPosition { get; set; } = new();
}
