using System.Collections.Generic;
using System.Globalization;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.HostRules;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The host-administration command group: <c>/gamerule</c>, <c>/kick</c>,
/// <c>/ban</c>, <c>/unban</c>. It is its own owner because it is a separate
/// responsibility — mutating the session's membership and its rules — and
/// because a command group is how the console grows: the registry discovers a new
/// group's <see cref="ConsoleCommandAttribute"/> methods from the owner it is
/// handed, so adding commands never means growing <see cref="CommandConsoleService"/>
/// past the architecture gate.
///
/// Permission is declared per command (<see cref="CommandPermission.HostOnly"/>) and
/// the console enforces it before the handler runs; the membership services behind
/// <c>/kick</c>, <c>/ban</c> and <c>/unban</c> refuse a non-host again, so a permission
/// bug there cannot become a removal. <c>/gamerule</c> writes this process's own config
/// entry, so its gate is the console's alone — a non-host that reached it would change
/// only its own config, which the host's announced set overrides (decision 263).
/// </summary>
internal sealed class HostAdminCommands(
	IHostBanService hostBans,
	ISessionControl session,
	IHostRules hostRules,
	IHostRulesEditor hostRulesEditor,
	ILogger log)
{
	private readonly IHostBanService _hostBans = hostBans;
	private readonly ISessionControl _session = session;
	private readonly IHostRules _hostRules = hostRules;
	private readonly IHostRulesEditor _hostRulesEditor = hostRulesEditor;
	private readonly ILogger _log = log;

	/// <summary>
	/// The host-rule verb, shaped after Minecraft's <c>gamerule &lt;rule name&gt; [&lt;value&gt;]</c>
	/// (the live Java Edition syntax, fetched 2026-10-23): the value is optional and omitting it
	/// queries the rule; a rule name is one of the census's own lowercase names and is case-sensitive;
	/// a value is the rule's own kind and is refused by naming the accepted set. The write goes
	/// through the same config entry as the Admin page, so decision 263's change edge announces the
	/// new set to every member on the next pump. Host-only: the rules are the host's own switches, and
	/// this build has no member-to-host rule-write path.
	/// </summary>
	[ConsoleCommand("gamerule", "Host only: read or set one host rule, shaped like Minecraft's /gamerule.", CommandPermission.HostOnly, "/gamerule <rule name> [<value>]", CommandArgumentKind.Text, CommandArgumentKind.Text)]
	internal string GameRule(IReadOnlyList<string> args)
	{
		if (args.Count is < 2 or > 3)
		{
			return "Usage: /gamerule <rule name> [<value>]";
		}

		var row = HostRuleCensus.Find(args[1]);
		if (row is null)
		{
			_log.LogInformation("[Command] /gamerule refused the unknown rule name '{Name}'.", args[1]);
			return $"Unknown game rule '{args[1]}'. Known rules: {string.Join(", ", HostRuleCensus.Names)}.";
		}

		if (args.Count == 2)
		{
			var current = HostRuleValueText.Describe(HostRuleSet.From(_hostRules), row);
			_log.LogDebug("[Command] /gamerule queried {Rule} = {Value}.", row.Name, current);
			return $"Game rule {row.Name} is currently set to: {current}.";
		}

		if (!HostRuleValueText.TryParse(row, args[2], out var value, out var error))
		{
			_log.LogInformation("[Command] /gamerule refused {Rule} = '{Text}': {Reason}", row.Name, args[2], error);
			return $"Game rule {row.Name}: {error}";
		}

		if (!_hostRulesEditor.TrySet(row.Id, value, out var refusal))
		{
			_log.LogWarning("[Command] /gamerule could not write {Rule} = {Value}: {Reason}", row.Name, value, refusal);
			return $"Game rule {row.Name} was not changed: {refusal}";
		}

		_log.LogInformation("[Command] /gamerule set {Rule} = {Value}.", row.Name, value);
		return $"Game rule {row.Name} is now set to: {value}.";
	}

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
