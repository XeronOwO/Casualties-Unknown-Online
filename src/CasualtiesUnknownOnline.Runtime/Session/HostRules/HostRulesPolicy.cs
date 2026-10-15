using CasualtiesUnknownOnline.Runtime.Configuration;

namespace CasualtiesUnknownOnline.Runtime.Session.HostRules;

/// <summary>
/// Pure host-rule decision surface, L0-testable without live services: the decisions
/// the HOST takes on its own rules (who may join a running world, whether it continues
/// to the next layer). The rules themselves are also announced to members
/// (<c>HostRuleCensus</c>, <see cref="HostRulesAnnouncer"/>); what this class answers is
/// the host-side verdict, which no announcement turns into a member-side one.
/// </summary>
internal static class HostRulesPolicy
{
	/// <summary>
	/// Whether a brand-new member may handshake while the host is already in a
	/// world. Reconnects and pre-world/menu/generating joins are always allowed;
	/// this only gates the "join a running co-op world" case.
	/// </summary>
	internal static bool CanAcceptNewMember(HostRulesOptions rules, bool hostLocalInWorld) =>
		CanAcceptNewMember(rules.AllowLateJoin, hostLocalInWorld);

	internal static bool CanAcceptNewMember(bool allowLateJoin, bool hostLocalInWorld) =>
		allowLateJoin || !hostLocalInWorld;

	/// <summary>The host may automatically continue to the next layer.</summary>
	internal static bool CanAutoContinue(HostRulesOptions rules) => rules.AutoContinue;
}
