using CasualtiesUnknownOnline.Application.Kernel;
using CasualtiesUnknownOnline.Runtime.Session.HostRules;

namespace CasualtiesUnknownOnline.Runtime.Session.Permissions;

/// <summary>
/// The host's own member-command rules, answered for the Application layer's
/// admission seam (<see cref="IMemberCommandRules"/>). Two questions, one owner,
/// because both are the HOST's ruling on a member's submission rather than a
/// property of the build: the cheat SWITCH a host configures
/// (<see cref="IHostRules.AllowMemberCheatVerbs"/>) and the cheat ROSTER a host edits
/// with <c>/op</c>/<c>/deop</c> (<see cref="CheatPermissions"/>). A member needs both,
/// which is the user's 2026-10-18 ruling.
///
/// <para>
/// It is a separate one-member class rather than a member on <c>IHostRules</c>
/// itself because the direction is the point: the seam may not reference the
/// Runtime's configuration types, so the Application declares the question and this
/// is the Runtime's answer. Reading each ruling at access time keeps the hot-reload
/// behaviour every other host rule has — a host that closes the switch or revokes a
/// grant mid-session refuses the next request and leaves the committed ones alone.
/// </para>
/// </summary>
internal sealed class SessionMemberCommandRules(IHostRules hostRules, CheatPermissions cheatPermissions) : IMemberCommandRules
{
	private readonly IHostRules _hostRules = hostRules;
	private readonly CheatPermissions _cheatPermissions = cheatPermissions;

	public bool CheatVerbsOpenToMembers => _hostRules.AllowMemberCheatVerbs;

	public bool AllowsCheatVerb(ulong steamId) => _cheatPermissions.IsGranted(steamId);
}
