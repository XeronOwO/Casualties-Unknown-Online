using System;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.HostRules;

/// <summary>
/// The host rules as THIS client last received them — the member side of the
/// announcement. It holds one absolute set, replaced wholesale on every arrival, so a
/// member that missed one announcement is put right by the next one and a repeat is
/// idempotent.
///
/// <para>
/// The set belongs to the SESSION that announced it, and it is dropped when that
/// session ends (<see cref="ResetSessionState"/>, decision 3's session-owned state): a
/// process that was a member and later hosts, or joins a different host, would
/// otherwise read the previous host's rules as its own — enforcing rules nobody chose
/// for it, offering controls that host's policy forbids, and announcing that host's
/// values to its own members. Every role transition in this tree leaves the old session
/// through <c>SessionService.TeardownSession</c> (the two host entry points
/// <c>OnLobbyCreated</c> / <c>OnLobbyEntered</c> and <c>OnLobbyLeft</c> all reach it),
/// which is what fires the edge this store listens to.
/// </para>
///
/// <para>
/// Nothing announced is a state with its own meaning, and it is not "everything is
/// allowed": <see cref="Current"/> stays null and <see cref="Forbids"/> answers false,
/// which SKIPS the local check and leaves the host's own verdict as the only one —
/// exactly the behaviour of a client that never received the set (a version skew, a
/// swallowed send, a build without the announcement at all, or a member that has just
/// joined a new host and has not been announced to yet). The fallback is the model that
/// existed before the rules were replicated, never a guess at what the host might have
/// meant.
/// </para>
///
/// <para>
/// The local check is an interception layer over the operator, never the verdict: it
/// can refuse a gesture the announced rules forbid (so a forbidden write does not
/// happen and does not have to be undone), and it can never be the reason something is
/// ALLOWED — the host checks the same thing behind it and its refusal is the one that
/// settles the record.
/// </para>
/// </summary>
public sealed class AnnouncedHostRules : ISessionReset, IDisposable
{
	private readonly ISessionControl _session;
	private readonly ILogger<AnnouncedHostRules> _log;
	private readonly object _sync = new();
	private HostRuleSet? _current;

	public AnnouncedHostRules(ISessionControl session, ILogger<AnnouncedHostRules> log)
	{
		_session = session;
		_log = log;
		_session.SessionEnded += ResetSessionState;
	}

	/// <summary>The announced absolute set, or null when this client holds none.</summary>
	public HostRuleSet? Current
	{
		get
		{
			lock (_sync)
			{
				return _current;
			}
		}
	}

	/// <summary>
	/// The local check for one FLAG rule: true only when a set has arrived AND it says
	/// the rule is off. Throws for a rule that is not a flag — a gesture is refused on
	/// a flag, and asking the wrong shape must fail rather than read a neighbouring
	/// value's meaning.
	/// </summary>
	public bool Forbids(HostRuleId rule) => Current is { } announced && !announced.Boolean(rule);

	/// <summary>
	/// Applies an announced set (the receive handler's own call). Returns whether the
	/// set CHANGED — a repeat announcement is absorbed silently, which is what makes
	/// the world-entry group and the in-session repair group safe to re-send.
	/// </summary>
	internal bool Apply(HostRuleSet set)
	{
		lock (_sync)
		{
			if (_current == set)
			{
				return false;
			}

			_current = set;
			return true;
		}
	}

	/// <summary>
	/// The session that announced the set has ended: drop it, so the next session starts
	/// from this process's own configuration and the local check is skipped until that
	/// host announces. Logged only when there was something to drop — the ordinary
	/// session end of a process that never received a set stays silent.
	/// </summary>
	public void ResetSessionState()
	{
		lock (_sync)
		{
			if (_current is null)
			{
				return;
			}

			_current = null;
		}

		_log.LogInformation("[Rules] the session ended — the announced host rules are dropped; this client reads its own entries, and skips the local check, until the next host announces.");
	}

	public void Dispose() => _session.SessionEnded -= ResetSessionState;
}
