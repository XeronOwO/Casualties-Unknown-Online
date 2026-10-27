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
/// share. Every answer names its kind, so a refusal ("unknown member", "could not ban")
/// prints in the error colour.
/// </para>
/// </summary>
internal sealed class HostAdminCommands(
	IHostBanService hostBans,
	ISessionControl session)
{
	private readonly IHostBanService _hostBans = hostBans;
	private readonly ISessionControl _session = session;

	[ConsoleCommand("kick", "Host only: kick a member by SteamId or display name.", CommandPermission.HostOnly, "/kick <steamId|displayName>", CommandArgumentKind.PlayerOrSteamId)]
	internal ConsoleAnswer Kick(IReadOnlyList<string> args)
	{
		if (args.Count < 2)
		{
			return ConsoleAnswer.Refused("Usage: /kick <steamId|displayName>");
		}

		if (!CommandMemberTargets.TryResolve(_session, args[1], out var steamId))
		{
			return ConsoleAnswer.Refused($"Unknown member '{args[1]}'.", "admin.answer.unknown_member", args[1]);
		}

		if (!_session.KickMember(steamId, "kicked via console"))
		{
			return ConsoleAnswer.Refused($"Could not kick {steamId} — not a removable guest, or already left.", "admin.answer.refused_kick", Id(steamId));
		}

		return ConsoleAnswer.Ok($"Kicked member {steamId}.", "admin.answer.kicked", Id(steamId));
	}

	[ConsoleCommand("ban", "Host only: ban a member by SteamId or display name.", CommandPermission.HostOnly, "/ban <steamId|displayName>", CommandArgumentKind.PlayerOrSteamId)]
	internal ConsoleAnswer Ban(IReadOnlyList<string> args)
	{
		if (args.Count < 2)
		{
			return ConsoleAnswer.Refused("Usage: /ban <steamId|displayName>");
		}

		if (!CommandMemberTargets.TryResolve(_session, args[1], out var steamId))
		{
			return ConsoleAnswer.Refused($"Unknown member '{args[1]}'.", "admin.answer.unknown_member", args[1]);
		}

		if (!_hostBans.Ban(steamId, "banned via console"))
		{
			return ConsoleAnswer.Refused($"Could not ban {steamId} — not a removable guest, already banned, or not host.", "admin.answer.refused_ban", Id(steamId));
		}

		return ConsoleAnswer.Ok($"Banned member {steamId}.", "admin.answer.banned", Id(steamId));
	}

	[ConsoleCommand("unban", "Host only: unban a SteamId.", CommandPermission.HostOnly, "/unban <steamId>", CommandArgumentKind.SteamId)]
	internal ConsoleAnswer Unban(IReadOnlyList<string> args)
	{
		if (args.Count < 2)
		{
			return ConsoleAnswer.Refused("Usage: /unban <steamId>");
		}

		if (!ulong.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var steamId))
		{
			return ConsoleAnswer.Refused($"SteamId '{args[1]}' is not a number.", "admin.answer.refused_not_a_number", args[1]);
		}

		if (!_hostBans.Unban(steamId))
		{
			return ConsoleAnswer.Refused($"Could not unban {steamId} — not in the ban list.", "admin.answer.refused_unban", Id(steamId));
		}

		return ConsoleAnswer.Ok($"Unbanned {steamId}.", "admin.answer.unbanned", Id(steamId));
	}

	/// <summary>A Steam id as the catalogue's argument form (invariant digits, so the sentence reads the same on every machine).</summary>
	private static string Id(ulong steamId) => steamId.ToString(CultureInfo.InvariantCulture);
}
