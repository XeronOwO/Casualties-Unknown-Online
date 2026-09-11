using System;
using System.Collections.Generic;
using System.Globalization;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.HostRules;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The host-administration command group: <c>/hostrules</c>, <c>/kick</c>,
/// <c>/ban</c>, <c>/unban</c>. It is its own owner because it is a separate
/// responsibility — mutating the session's membership and its rules — and
/// because a command group is how the console grows: the registry discovers a new
/// group's <see cref="ConsoleCommandAttribute"/> methods from the owner it is
/// handed, so adding commands never means growing <see cref="CommandConsoleService"/>
/// past the architecture gate.
///
/// Permission is declared per command (<see cref="CommandPermission.HostOnly"/>);
/// the console enforces it before the handler runs, and every service behind these
/// handlers refuses a non-host again, so a permission bug cannot become a write.
/// </summary>
internal sealed class HostAdminCommands(
	IHostBanService hostBans,
	ISessionControl session,
	IHostRulesEditor hostRulesEditor,
	ILogger log)
{
	private readonly IHostBanService _hostBans = hostBans;
	private readonly ISessionControl _session = session;
	private readonly IHostRulesEditor _hostRulesEditor = hostRulesEditor;
	private readonly ILogger _log = log;

	[ConsoleCommand("hostrules", "Host only: update host rules from a JSON object.", CommandPermission.HostOnly, "/hostrules <json>", CommandArgumentKind.Json)]
	internal string HostRules(IReadOnlyList<string> args)
	{
		if (args.Count < 2)
		{
			return "Usage: /hostrules <json>";
		}

		if (!HostRulesJsonApplier.TryApply(args[1], _hostRulesEditor, out var updated, out var error))
		{
			return error ?? "Could not apply host rules.";
		}

		_log.LogInformation("[Command] /hostrules updated {Count} host-rule setting(s).", updated);
		return $"Updated {updated} host rule(s).";
	}

	[ConsoleCommand("kick", "Host only: kick a member by SteamId or display name.", CommandPermission.HostOnly, "/kick <steamId|displayName>", CommandArgumentKind.PlayerOrSteamId)]
	internal string Kick(IReadOnlyList<string> args)
	{
		if (args.Count < 2)
		{
			return "Usage: /kick <steamId|displayName>";
		}

		if (!TryResolveMember(args[1], out var steamId))
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

		if (!TryResolveMember(args[1], out var steamId))
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

	private bool TryResolveMember(string text, out ulong steamId)
	{
		if (ulong.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out steamId))
		{
			return true;
		}

		if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
			&& ulong.TryParse(text.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out steamId))
		{
			return true;
		}

		foreach (var member in _session.Members)
		{
			if (!string.IsNullOrWhiteSpace(member.DisplayName)
				&& string.Equals(member.DisplayName.Trim(), text, StringComparison.OrdinalIgnoreCase))
			{
				steamId = member.SteamId;
				return true;
			}
		}

		steamId = 0;
		return false;
	}
}
