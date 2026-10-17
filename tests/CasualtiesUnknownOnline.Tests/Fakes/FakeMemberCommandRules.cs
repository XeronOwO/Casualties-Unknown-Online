using System.Collections.Generic;
using CasualtiesUnknownOnline.Application.Kernel;

namespace CasualtiesUnknownOnline.Tests.Fakes;

/// <summary>
/// The host's own member-command rules (<see cref="IMemberCommandRules"/>) as a
/// test's own switch. Both defaults are the product's defaults — the cheat switch
/// CLOSED and the cheat roster EMPTY — so a suite that does not care about either
/// exercises the posture a host actually ships with, and the console-verb cases ask
/// for what they mean explicitly.
/// </summary>
internal sealed class FakeMemberCommandRules : IMemberCommandRules
{
	/// <summary>Whether the host's cheat SWITCH is open to members. False, exactly like <c>HostRulesOptions.AllowMemberCheatVerbs</c>.</summary>
	internal bool AllowCheatVerbs { get; set; }

	/// <summary>The cheat roster: nobody by default, exactly like a session that never ran <c>/op</c>.</summary>
	internal HashSet<ulong> CheatGrants { get; } = [];

	public bool CheatVerbsOpenToMembers => AllowCheatVerbs;

	public bool AllowsCheatVerb(ulong steamId) => CheatGrants.Contains(steamId);
}
