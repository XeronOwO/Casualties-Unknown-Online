using System.Collections.Generic;
using System.Globalization;
using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The host-administration command group: <c>/kick</c>, <c>/ban</c>, <c>/unban</c>. It is
/// its own owner because it is a separate responsibility — mutating the session's
/// MEMBERSHIP — and because a command group is how the console grows: the registry
/// discovers a new group's <see cref="ConsoleCommandAttribute"/> methods from the owner it
/// is handed, so adding commands never means growing <see cref="CommandConsoleService"/>
/// past the architecture gate.
///
/// <para>
/// Every verb here is <see cref="CommandPermission.HostOnly"/> and the membership services
/// behind them refuse a non-host again, so a permission bug cannot become a removal. The
/// host rules are NOT here: <c>/gamerule</c> is a cheat-family verb with its own gate and
/// its own command group (<see cref="HostRuleCommands"/>), because a member the host grants
/// may change a rule through the host — a permission this group's host-only verbs do not
/// share.
/// </para>
/// </summary>
internal sealed class HostAdminCommands(
	IHostBanService hostBans,
	ISessionControl session)
{
	private readonly IHostBanService _hostBans = hostBans;
	private readonly ISessionControl _session = session;

	[ConsoleCommand("kick", "Host only: kick a member by SteamId or display name.", CommandPermission.HostOnly, "/kick <steamId|displayName>", CommandArgumentKind.PlayerOrSteamId)]
	internal string Kick(IReadOnlyList<string> args)
	{
		if (args.Count < 2)
		{
			return "Usage: /kick <steamId|displayName>";
		}

		if (!CommandMemberTargets.TryResolve(_session, args[1], out var steamId))
		{
			return $"Unknown member '{args[1]}'.";
		}

		if (!_session.KickMember(steamId, "kicked via console"))
		{
			return $"Could not kick {steamId} — not a removable guest, or already left.";
		}

		return $"Kicked member {steamId}.";
	}

	[ConsoleCommand("ban", "Host only: ban a member by SteamId or display name.", CommandPermission.HostOnly, "/ban <steamId|displayName>", CommandArgumentKind.PlayerOrSteamId)]
	internal string Ban(IReadOnlyList<string> args)
	{
		if (args.Count < 2)
		{
			return "Usage: /ban <steamId|displayName>";
		}

		if (!CommandMemberTargets.TryResolve(_session, args[1], out var steamId))
		{
			return $"Unknown member '{args[1]}'.";
		}

		if (!_hostBans.Ban(steamId, "banned via console"))
		{
			return $"Could not ban {steamId} — not a removable guest, already banned, or not host.";
		}

		return $"Banned member {steamId}.";
	}

	[ConsoleCommand("unban", "Host only: unban a SteamId.", CommandPermission.HostOnly, "/unban <steamId>", CommandArgumentKind.SteamId)]
	internal string Unban(IReadOnlyList<string> args)
	{
		if (args.Count < 2)
		{
			return "Usage: /unban <steamId>";
		}

		if (!ulong.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var steamId))
		{
			return $"SteamId '{args[1]}' is not a number.";
		}

		if (!_hostBans.Unban(steamId))
		{
			return $"Could not unban {steamId} — not in the ban list.";
		}

		return $"Unbanned {steamId}.";
	}
}
