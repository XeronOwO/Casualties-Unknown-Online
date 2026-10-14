using CasualtiesUnknownOnline.Application.Kernel;

namespace CasualtiesUnknownOnline.Runtime.Session.HostRules;

/// <summary>
/// The host's own member-command rules, answered from <see cref="IHostRules"/> for
/// the Application layer's admission seam (<see cref="IMemberCommandRules"/>).
///
/// <para>
/// It is a separate one-member class rather than a member on <c>IHostRules</c>
/// itself because the direction is the point: the seam may not reference the
/// Runtime's configuration types, so the Application declares the question and this
/// is the Runtime's answer. Reading the rule at property access time keeps the
/// hot-reload behaviour every other host rule has — a host that flips the flag
/// mid-session refuses the next write and leaves the committed ones alone.
/// </para>
/// </summary>
internal sealed class HostRulesMemberCommandRules(IHostRules hostRules) : IMemberCommandRules
{
	private readonly IHostRules _hostRules = hostRules;

	public bool AllowsItemDataWrite => _hostRules.AllowMemberItemDataWrite;
}
