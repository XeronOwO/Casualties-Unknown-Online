using CasualtiesUnknownOnline.Application.Kernel;

namespace CasualtiesUnknownOnline.Tests.Fakes;

/// <summary>
/// The host's own member-command rules (<see cref="IMemberCommandRules"/>) as a
/// test's own switch. The default is the product's default — OFF — so a suite that
/// does not care about the rule exercises the posture a host actually ships with,
/// and the console-verb cases ask for the open one explicitly.
/// </summary>
internal sealed class FakeMemberCommandRules : IMemberCommandRules
{
	/// <summary>Whether the host's rule opens the item-data write verbs to members. False, exactly like <c>HostRulesOptions.AllowMemberItemDataWrite</c>.</summary>
	internal bool AllowItemDataWrite { get; set; }

	public bool AllowsItemDataWrite => AllowItemDataWrite;
}
