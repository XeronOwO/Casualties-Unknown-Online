using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Permissions;

/// <summary>
/// The cheat-permission roster as THIS client last received it — the member side of
/// the host's announcement. One absolute list, replaced wholesale on every arrival,
/// so a member that missed one announcement is put right by the next one and a
/// repeat is idempotent.
///
/// <para>
/// It belongs to the SESSION that announced it and is dropped when that session ends
/// (<see cref="ResetSessionState"/>): a process that was a member and later hosts,
/// or that joins a different host, must not enforce the previous host's grants on
/// itself.
/// </para>
///
/// <para>
/// Nothing announced is a state with its own meaning, and it is NOT "nobody is
/// granted": <see cref="Current"/> stays null and <see cref="Refuses"/> answers
/// false, which SKIPS the local check and leaves the host's verdict as the only one —
/// exactly the behaviour of a client that never received the list (a swallowed send,
/// or a build without the announcement). The fallback is the model that existed before
/// the list was replicated, never a guess at what the host might have meant.
/// </para>
///
/// <para>
/// The local check is an interception over the operator, never the verdict: it can
/// refuse a cheat verb the announced list closes (so a forbidden verb costs no round
/// trip and nothing has to be settled back), and it can never be the reason something
/// is ALLOWED — the host judges every request that reaches it against its own roster,
/// and its refusal is the one that settles the record.
/// </para>
/// </summary>
public sealed class AnnouncedCheatPermissions : ISessionReset, IDisposable
{
	private readonly ISessionControl _session;
	private readonly ILogger<AnnouncedCheatPermissions> _log;
	private readonly object _sync = new();
	private ulong[]? _current;

	public AnnouncedCheatPermissions(ISessionControl session, ILogger<AnnouncedCheatPermissions> log)
	{
		_session = session;
		_log = log;
		_session.SessionEnded += ResetSessionState;
	}

	/// <summary>
	/// The announced absolute list, or null when this client holds none. It is an
	/// OBSERVABILITY surface (the Admin summary and the tests read it); the local
	/// decision is <see cref="Refuses"/>, which is what the gate calls.
	/// </summary>
	public IReadOnlyList<ulong>? Current
	{
		get
		{
			lock (_sync)
			{
				return _current is null ? null : [.. _current];
			}
		}
	}

	/// <summary>
	/// The local check: true only when a list has arrived AND it does not name this
	/// player. With nothing announced it answers false (skip the check) rather than
	/// treating the absence as a closed list.
	/// </summary>
	public bool Refuses(ulong steamId)
	{
		lock (_sync)
		{
			return _current is not null && Array.IndexOf(_current, steamId) < 0;
		}
	}

	/// <summary>
	/// Applies an announced list (the receive handler's own call). Returns whether the
	/// list CHANGED — a repeat announcement is absorbed silently, which is what makes
	/// the world-entry group and the in-session repair group safe to re-send. The list
	/// is stored SORTED so a grant order that moved is not a change.
	/// </summary>
	internal bool Apply(IReadOnlyList<ulong> granted)
	{
		var sorted = granted.OrderBy(id => id).ToArray();
		lock (_sync)
		{
			if (_current is not null && _current.SequenceEqual(sorted))
			{
				return false;
			}

			_current = sorted;
			return true;
		}
	}

	/// <summary>
	/// The session that announced the list has ended: drop it, so the next session
	/// starts from no list (the local check skipped) until that host announces.
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

		_log.LogInformation("[Cheat] the session ended — the announced cheat-permission list is dropped; this client skips the local check until the next host announces.");
	}

	public void Dispose() => _session.SessionEnded -= ResetSessionState;
}
