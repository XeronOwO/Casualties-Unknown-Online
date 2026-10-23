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
/// The same decision has a QUIET form (<see cref="MayRun"/>, <see cref="TryRefuseQuiet"/>):
/// the console's completion and `/help` filter ask it, and it writes no log because those
/// questions are asked per keystroke and per list row. The refusing path
/// (<see cref="TryRefuse"/>) keeps its one log line per refusal; the two forms run the SAME
/// <see cref="Judge"/>, so the filter and the handler can never disagree.
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

	/// <summary>Why the verb was refused, so the refusing path can log at the level each ground deserves.</summary>
	private enum Ground
	{
		None,
		NoSession,
		SwitchClosed,
		NotOnRoster,
	}

	/// <summary>One judge's answer: the ground and the sentence the operator reads (empty when allowed).</summary>
	private readonly record struct Verdict(Ground Ground, string Refusal)
	{
		internal bool Allowed => Ground == Ground.None;
	}

	/// <summary>
	/// Whether this client may run <paramref name="verb"/> right now — the quiet form the console's
	/// completion and <c>/help</c> filter ask. Writes no log; the sentence is not needed here.
	/// </summary>
	internal bool MayRun(string verb) => Judge(verb).Allowed;

	/// <summary>
	/// The quiet REFUSAL form: the sentence the verb would print, without the log line. <c>/help</c>
	/// answers a hidden command with exactly this, so a player never learns a command by help that the
	/// console then refuses as unknown.
	/// </summary>
	internal bool TryRefuseQuiet(string verb, out string refusal)
	{
		var verdict = Judge(verb);
		refusal = verdict.Refusal;
		return !verdict.Allowed;
	}

	/// <summary>
	/// Whether this client must refuse <paramref name="verb"/> right now, with the sentence the operator
	/// reads. False means the verb may proceed — never that it will land, which is the host's answer. One
	/// log line per refusal, at the ground's level; the decision itself is <see cref="Judge"/>.
	/// </summary>
	internal bool TryRefuse(string verb, out string refusal)
	{
		var verdict = Judge(verb);
		refusal = verdict.Refusal;
		switch (verdict.Ground)
		{
			case Ground.None:
				return false;
			case Ground.NoSession:
				// No session, no cheat permission — and this covers BOTH roles by one rule:
				// the host's own standing IS the online switch, and a member's standing is a
				// grant for a session.
				_log.LogWarning("[Cheat] /{Verb} refused locally: no active session ({Role}).", verb, _session.Role);
				return true;
			case Ground.SwitchClosed:
				// The broader ruling, checked first: the host has announced that the cheat
				// verbs are not open to members at all, so a grant would not help either.
				_log.LogInformation("[Cheat] /{Verb} refused locally: the host's announced cheat switch {Rule}=false closes the family to every member; nothing was applied or sent.",
					verb, HostRuleId.AllowMemberCheatVerbs);
				return true;
			default:
				_log.LogInformation("[Cheat] /{Verb} refused locally: the host's announced roster does not name {Local} — nothing was applied or sent.",
					verb, _session.LocalSteamId);
				return true;
		}
	}

	/// <summary>
	/// The one decision both forms run. No logging, no side effects: a completion filter must be able to
	/// ask it on every keystroke and a help row on every line without producing the refusal's log line.
	/// </summary>
	private Verdict Judge(string verb)
	{
		if (!_session.SessionActive)
		{
			return new Verdict(Ground.NoSession, $"No active session — a cheat verb needs one: the host's own permission is the online switch, and a member's is the host's grant for the session. /{verb} did nothing here.");
		}

		if (_session.Role == SessionRole.Host)
		{
			return new Verdict(Ground.None, "");
		}

		if (_hostRules.Forbids(HostRuleId.AllowMemberCheatVerbs))
		{
			return new Verdict(Ground.SwitchClosed, $"Refused locally: the host's cheat switch ({HostRuleId.AllowMemberCheatVerbs}) is off, so no member may run a cheat verb (the host announces its rules to this client). Nothing was done here.");
		}

		if (_announced.Refuses(_session.LocalSteamId))
		{
			return new Verdict(Ground.NotOnRoster, $"Refused locally: the host's cheat roster (announced to this client) does not name you, so /{verb} cannot land. The host grants it with /op. Nothing was done here.");
		}

		// Either the announced roster names this client, or nothing was announced and the host's own
		// verdict is the only one — the two cases the ticket keeps apart on purpose.
		return new Verdict(Ground.None, "");
	}
}
