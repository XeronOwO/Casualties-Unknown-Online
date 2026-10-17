using System;
using System.Collections.Generic;
using System.Globalization;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Permissions;

/// <summary>
/// The host side of the cheat-roster replication: one absolute announcement per edge.
/// FOUR edges reach it, and each exists for a different member state:
/// <list type="bullet">
/// <item>the member's handshake completing (<see cref="ISessionControl.MemberAdded"/>)
/// is the first — a member may type a cheat verb from the menu, and the console's own
/// refusal has to be the one it reads there rather than a round trip;</item>
/// <item>the world-entry group and (third) the in-session repair group call
/// <see cref="Announce"/> like every other absolute table, which is what makes a member
/// whose admission announcement the lazy peer session swallowed converge;</item>
/// <item>a grant or a revoke while the session runs calls <see cref="AnnounceAll"/>
/// (the fourth), because the list governs verbs members type in the world they are
/// already in — and a repeated grant re-announces too, so a member holding a stale
/// closed list converges without waiting for the repair cycle.</item>
/// </list>
///
/// <para>
/// A guest never announces: its own roster stays empty (only the host edits one), and
/// the role guard is what keeps a member's process from broadcasting a list it was
/// told as if it were the session's. Every send happens on the thread that raised the
/// edge — the receive path (pump) for the admission edge and the console for a
/// grant/revoke — so no deferral is needed, unlike the host rules' config-change edge.
/// </para>
/// </summary>
public sealed class CheatPermissionAnnouncer : ICheatPermissionAnnouncement, IDisposable
{
	private readonly ISessionControl _session;
	private readonly CheatPermissions _permissions;
	private readonly PacketSender _sender;
	private readonly ILogger<CheatPermissionAnnouncer> _log;

	public CheatPermissionAnnouncer(
		ISessionControl session,
		CheatPermissions permissions,
		PacketSender sender,
		ILogger<CheatPermissionAnnouncer> log)
	{
		_session = session;
		_permissions = permissions;
		_sender = sender;
		_log = log;
		_session.MemberAdded += OnMemberAdded;
	}

	public void Dispose() => _session.MemberAdded -= OnMemberAdded;

	public void Announce(ulong steamId)
	{
		if (_session.Role != SessionRole.Host)
		{
			_log.LogWarning("[Cheat] a roster announcement for {Peer} was requested while this process is {Role} — ignored; only a host announces its roster.",
				steamId, _session.Role);
			return;
		}

		var granted = _permissions.Granted;
		_sender.Send(steamId, NetMsg.CheatPermissions, CheatPermissionsMsg.From(granted));
		_log.LogInformation("[Cheat] announced the cheat roster to {Peer} ({Count} member(s): {List}).",
			steamId, granted.Count, Format(granted));
	}

	public void AnnounceAll()
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		var members = new List<ulong>();
		foreach (var member in _session.Members)
		{
			members.Add(member.SteamId);
		}

		_log.LogInformation("[Cheat] the roster changed — announcing it to {Count} member(s).", members.Count);
		foreach (var steamId in members)
		{
			Announce(steamId);
		}
	}

	/// <summary>
	/// The admission edge: the member's handshake completed, so it stays a member for
	/// the rest of the session and this is the only announcement that reaches it before
	/// its first world entry.
	/// </summary>
	private void OnMemberAdded(ulong steamId)
	{
		if (_session.Role != SessionRole.Host)
		{
			// The guest side raises the same event for the HOST member. Nothing to send.
			return;
		}

		Announce(steamId);
	}

	private static string Format(IReadOnlyList<ulong> granted) =>
		granted.Count == 0
			? "nobody"
			: string.Join(", ", FormatMembers(granted));

	private static IEnumerable<string> FormatMembers(IReadOnlyList<ulong> granted)
	{
		foreach (var steamId in granted)
		{
			yield return steamId.ToString(CultureInfo.InvariantCulture);
		}
	}
}
