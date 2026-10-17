using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Networking;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Permissions;

/// <summary>
/// The HOST's cheat-permission roster: the members this host has granted the cheat
/// verbs to (<c>/data</c>'s write verbs today, <c>/give</c> as it lands). It is the
/// WHO half of the host's two cheat rulings: the family must also be OPEN, which is
/// <see cref="Configuration.HostRulesOptions.AllowMemberCheatVerbs"/> — a grant alone
/// admits nothing, and a closed switch refuses even a granted member (the user's
/// 2026-10-18 ruling). Editing it is <c>/op</c> and <c>/deop</c>, and its whole content
/// is announced to every member through <see cref="CheatPermissionAnnouncer"/> so a
/// member's own console can refuse a cheat verb by name before anything happens.
///
/// <para>
/// The list is the host's ruling on ORDINARY clients, never an anti-cheat boundary:
/// a modified client can type its own console, so every request that does arrive is
/// still judged here (the kernel's admission seam for the item-data write, the
/// give-verb's own host half) — this roster answers WHO MAY ASK, and the host's one
/// family switch answers whether the family is open to members at all.
/// </para>
///
/// <para>
/// TWO standings it deliberately does not hold. The host's own permission is the
/// online switch (<see cref="ISessionControl.SessionActive"/>), never an entry here:
/// the host cannot op itself, and no member's departure revokes the host's own
/// standing. And an IP-DIRECT session has no roster at all: that transport knows a
/// peer by the logical id its own connection registered, not by an account, so a
/// grant keyed on it would follow a connection rather than a person — the truthful
/// answer is to refuse the grant, which <see cref="Grant"/> and <see cref="Revoke"/>
/// do by name rather than by leaving the list silently unusable.
/// </para>
///
/// <para>
/// The state belongs to the SESSION: it is dropped when the session ends
/// (<see cref="ResetSessionState"/>), so a process that was a member and later hosts,
/// or that rejoins a different host, never carries a stranger's grants into its own
/// session.
/// </para>
/// </summary>
public sealed class CheatPermissions : ISessionReset, IDisposable
{
	private readonly ISessionControl _session;
	private readonly ITransportIdentity _transport;
	private readonly ILogger<CheatPermissions> _log;
	private readonly object _sync = new();

	/// <summary>The granted ids, sorted, replaced whole on every edit so a reader never sees a half-built list.</summary>
	private ulong[] _granted = [];

	public CheatPermissions(ISessionControl session, ITransportIdentity transport, ILogger<CheatPermissions> log)
	{
		_session = session;
		_transport = transport;
		_log = log;
		_session.SessionEnded += ResetSessionState;
	}

	/// <summary>
	/// Whether this member may run the cheat verbs. The host's own standing is the
	/// online switch and is answered by the verb's own gate, never by this list.
	/// Read per submission at the host's admission seam, so a revoke takes effect on
	/// the next request and leaves the ones already committed alone.
	/// </summary>
	public bool IsGranted(ulong steamId)
	{
		lock (_sync)
		{
			return Array.IndexOf(_granted, steamId) >= 0;
		}
	}

	/// <summary>The list as an announcement sends it: a snapshot, sorted, never the live array.</summary>
	public IReadOnlyList<ulong> Granted
	{
		get
		{
			lock (_sync)
			{
				return [.. _granted];
			}
		}
	}

	/// <summary>
	/// Host only: add one member to the roster. Every refusal is a sentence the
	/// operator reads where it typed <c>/op</c>, because the five grounds (no session,
	/// IP-direct, the host itself, player 0, not a handshaken member) have five
	/// different remedies. "Already holds" is answered by the command, which
	/// re-announces the roster for exactly that case.
	/// </summary>
	public bool Grant(ulong steamId, out string refusal)
	{
		if (!CanEdit(steamId, out refusal))
		{
			return false;
		}

		lock (_sync)
		{
			if (Array.IndexOf(_granted, steamId) >= 0)
			{
				refusal = $"{steamId} already holds the cheat verbs.";
				return false;
			}

			_granted = Sorted([.. _granted, steamId]);
		}

		refusal = "";
		_log.LogInformation("[Cheat] granted the cheat verbs to {SteamId} — the roster now names {Count} member(s).", steamId, Granted.Count);
		return true;
	}

	/// <summary>Host only: remove one member from the roster. Refused by name when it holds no grant.</summary>
	public bool Revoke(ulong steamId, out string refusal)
	{
		if (!CanEdit(steamId, out refusal))
		{
			return false;
		}

		lock (_sync)
		{
			if (Array.IndexOf(_granted, steamId) < 0)
			{
				refusal = $"{steamId} does not hold the cheat verbs.";
				return false;
			}

			var kept = new List<ulong>(_granted.Length - 1);
			foreach (var granted in _granted)
			{
				if (granted != steamId)
				{
					kept.Add(granted);
				}
			}

			_granted = [.. kept];
		}

		refusal = "";
		_log.LogInformation("[Cheat] revoked the cheat verbs from {SteamId} — the roster now names {Count} member(s).", steamId, Granted.Count);
		return true;
	}

	/// <summary>
	/// The session that granted them ended: drop the roster. Logged only when there
	/// was something to drop, because the ordinary end of a session that never used
	/// the verbs stays silent.
	/// </summary>
	public void ResetSessionState()
	{
		lock (_sync)
		{
			if (_granted.Length == 0)
			{
				return;
			}

			_granted = [];
		}

		_log.LogInformation("[Cheat] the session ended — the cheat-permission roster is dropped; the next session starts from nobody.");
	}

	public void Dispose() => _session.SessionEnded -= ResetSessionState;

	/// <summary>
	/// The five standings a grant or a revoke may not be made on. The member check is
	/// the session's own presence table (the host is not in it, which is why the host's
	/// case is answered before it), and it must be HANDSHAKEN: a peer still completing
	/// its handshake has no identity this session has admitted yet.
	/// </summary>
	private bool CanEdit(ulong steamId, out string refusal)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			refusal = "Only the host edits the cheat roster, and only while its session runs.";
			return false;
		}

		if (_transport.IsIpDirect)
		{
			refusal = "This session is IP-direct: a peer is known by the logical id its own connection registered, not by an account, so a grant keyed on it would follow a connection rather than a person. Cheat permission is not granted in an IP-direct session.";
			return false;
		}

		if (steamId == _session.LocalSteamId)
		{
			refusal = "The host's own cheat permission is the online switch: it is on while the session runs, and the roster does not change it.";
			return false;
		}

		if (steamId == 0)
		{
			refusal = "Player 0 is nobody — name a member.";
			return false;
		}

		if (!_session.TryGetMember(steamId, out var member) || !member.Handshaken)
		{
			refusal = $"{steamId} is not a handshaken member of this session.";
			return false;
		}

		refusal = "";
		return true;
	}

	private static ulong[] Sorted(ulong[] ids)
	{
		Array.Sort(ids);
		return ids;
	}
}
