using CasualtiesUnknownOnline.Runtime.Session.HostRules;
using CasualtiesUnknownOnline.Runtime.Session.Permissions;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The CLIENT-side ("front end") check of the cheat family: the acting game instance
/// refuses a cheat verb locally, by name, before anything is applied or sent. It is
/// the shape the item-data write already takes — a closed permission costs no round
/// trip, and the operator reads the reason where they typed the verb.
///
/// <para>
/// THREE standings, one answer, in this order:
/// <list type="bullet">
/// <item>no active session — the host's own permission IS the online switch, and a
/// member's grant is a grant for a session, so neither role has a cheat standing
/// outside one;</item>
/// <item>the HOST — allowed from here; its standing is the online switch and no
/// roster entry (it cannot op itself);</item>
/// <item>a MEMBER — the host's two announced rulings: the cheat SWITCH must be open
/// (a closed switch refuses the family for everybody, granted or not), and then the
/// announced ROSTER must name this client. Each refusal names its own ground, so the
/// operator learns whether to ask for the switch or for a grant.</item>
/// </list>
/// </para>
///
/// <para>
/// With nothing announced, the check for that ruling is SKIPPED and the host's own
/// verdict settles the request — the fallback <see cref="AnnouncedCheatPermissions"/>
/// documents. The interception may never be the reason something the host would admit
/// is refused; a STALE announced value can be (a send the host's later grant never
/// corrected). The two halves heal differently: a repeated `/op` re-announces the
/// ROSTER, while a stale CLOSED SWITCH is announced with the host rules and waits for
/// the next entry, the repair cycle or a `/gamerule` write.
/// </para>
///
/// <para>
/// This is never the verdict. Every cheat verb that reaches the host is judged again
/// against the host's own switch and roster, and that refusal is the one both sides
/// end up agreeing on.
/// </para>
/// </summary>
internal sealed class CheatVerbGate(
	ISessionControl session,
	AnnouncedHostRules hostRules,
	AnnouncedCheatPermissions announced,
	ILogger log)
{
	private readonly ISessionControl _session = session;
	private readonly AnnouncedHostRules _hostRules = hostRules;
	private readonly AnnouncedCheatPermissions _announced = announced;
	private readonly ILogger _log = log;

	/// <summary>
	/// Whether this client must refuse <paramref name="verb"/> right now, with the
	/// sentence the operator reads. False means the verb may proceed — never that it
	/// will land, which is the host's answer.
	/// </summary>
	internal bool TryRefuse(string verb, out string refusal)
	{
		refusal = "";
		if (!_session.SessionActive)
		{
			// No session, no cheat permission — and this covers BOTH roles by one rule:
			// the host's own standing IS the online switch, and a member's standing is a
			// grant for a session.
			_log.LogWarning("[Cheat] /{Verb} refused locally: no active session ({Role}).", verb, _session.Role);
			refusal = $"No active session — a cheat verb needs one: the host's own permission is the online switch, and a member's is the host's grant for the session. /{verb} did nothing here.";
			return true;
		}

		if (_session.Role == SessionRole.Host)
		{
			return false;
		}

		if (_hostRules.Forbids(HostRuleId.AllowMemberCheatVerbs))
		{
			// The broader ruling, checked first: the host has announced that the cheat
			// verbs are not open to members at all, so a grant would not help either.
			_log.LogInformation("[Cheat] /{Verb} refused locally: the host's announced cheat switch {Rule}=false closes the family to every member; nothing was applied or sent.",
				verb, HostRuleId.AllowMemberCheatVerbs);
			refusal = $"Refused locally: the host's cheat switch ({HostRuleId.AllowMemberCheatVerbs}) is off, so no member may run a cheat verb (the host announces its rules to this client). Nothing was done here.";
			return true;
		}

		if (!_announced.Refuses(_session.LocalSteamId))
		{
			// Either the announced roster names this client, or nothing was announced and
			// the host's own verdict is the only one — the two cases the ticket keeps
			// apart on purpose.
			return false;
		}

		_log.LogInformation("[Cheat] /{Verb} refused locally: the host's announced roster does not name {Local} — nothing was applied or sent.",
			verb, _session.LocalSteamId);
		refusal = $"Refused locally: the host's cheat roster (announced to this client) does not name you, so /{verb} cannot land. The host grants it with /op. Nothing was done here.";
		return true;
	}
}
