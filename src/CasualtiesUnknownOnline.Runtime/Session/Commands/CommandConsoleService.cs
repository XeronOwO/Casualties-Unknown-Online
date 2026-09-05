using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Chat;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.HostRules;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using CasualtiesUnknownOnline.Runtime.Time;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The local in-game command/chat console. Commands are registered once at
/// construction from <see cref="ConsoleCommandAttribute"/>-marked methods into
/// an immutable command registry, parsed from a slash-prefixed line, gated by a
/// role permission, executed on this side, and answered into a bounded output
/// buffer. Non-command lines are forwarded to the existing text-chat domain so
/// the same input surface doubles as the reworked chat UI.
/// 
/// This is deliberately a local console: no new wire protocol, no packet handler,
/// no host relay. Host-only commands are enforced by role and use the existing
/// session/ban services on the host process.
/// </summary>
public sealed class CommandConsoleService : ICommandControl, ICommandCompletionSource, ICommandArgumentSuggestions
{
	private const int MaxLines = 200;

	private readonly IChatControl _chat;
	private readonly ISessionControl _session;
	private readonly IHostBanService _hostBans;
	private readonly IPlayerInteractionControl _playerInteraction;
	private readonly IEntitySyncControl _entities;
	private readonly IHostRulesEditor _hostRulesEditor;
	private readonly ITimeSource _time;
	private readonly ILogger<CommandConsoleService> _log;
	private readonly List<ConsoleLine> _lines = [];
	private readonly ConsoleCommandRegistry _commands;

	public CommandConsoleService(
		IChatControl chat,
		ISessionControl session,
		IHostBanService hostBans,
		IPlayerInteractionControl playerInteraction,
		IEntitySyncControl entities,
		IHostRulesEditor hostRulesEditor,
		ITimeSource time,
		ILogger<CommandConsoleService> log,
		ConsoleCommandRegistry commandRegistry)
	{
		_chat = chat;
		_session = session;
		_hostBans = hostBans;
		_playerInteraction = playerInteraction;
		_entities = entities;
		_hostRulesEditor = hostRulesEditor;
		_time = time;
		_log = log;
		_commands = commandRegistry;
		_chat.MessageReceived += OnChatLine;
		_session.SessionEnded += OnSessionEnded;
		_commands.AddBuiltIns(this);
		AddLine("CUO command console ready. Type /help for available commands, or just type to chat.", ConsoleLineKind.Info);
	}

	public IReadOnlyList<ConsoleLine> Lines => _lines;

	public IReadOnlyList<CommandSpec> Commands => _commands.ToSpecs();

	public IReadOnlyList<CommandSuggestion> Suggest(string input)
	{
		if (string.IsNullOrWhiteSpace(input))
		{
			return [];
		}

		var tokens = CommandLineTokenizer.Tokenize(input);
		if (tokens.Count == 0)
		{
			return [];
		}

		var isCommand = input.StartsWith("/", StringComparison.Ordinal);
		if (isCommand && tokens.Count == 1 && IsCommandTokenAtEnd(input, tokens[0]))
		{
			var partial = tokens[0].Unquoted;
			if (partial.StartsWith("/", StringComparison.Ordinal))
			{
				partial = partial.Substring(1);
			}

			return [.. _commands.All
				.Where(c => c.Name.StartsWith(partial, StringComparison.OrdinalIgnoreCase))
				.Select(c => new CommandSuggestion(c.Name, c.Description))
				.OrderBy(c => c.Text, StringComparer.OrdinalIgnoreCase)];
		}

		if (!isCommand)
		{
			return [];
		}

		var command = FindCommand(tokens[0].Unquoted);
		if (command is null)
		{
			return [];
		}

		var current = CommandLineTokenizer.CurrentToken(input);
		var argumentIndex = current.Length == 0 ? tokens.Count : tokens.Count - 1;
		if (argumentIndex < 1 || argumentIndex > command.ArgumentKinds.Count)
		{
			return [];
		}

		var tree = ConsoleCommandTree.FromArgumentKinds(command.ArgumentKinds);
		var kind = tree.GetArgumentKind(argumentIndex - 1);
		if (kind is null)
		{
			return [];
		}

		var prefix = current.Length == 0 ? "" : current.Unquoted;
		return SuggestFor(kind.Value, prefix);
	}

	public IReadOnlyList<CommandSuggestion> Suggest(CommandArgumentKind kind, string prefix) => SuggestFor(kind, prefix);

	public string? GetHint(string input)
	{
		if (string.IsNullOrWhiteSpace(input) || !input.StartsWith("/", StringComparison.Ordinal))
		{
			return null;
		}

		var tokens = CommandLineTokenizer.Tokenize(input);
		if (tokens.Count == 0)
		{
			return null;
		}

		var command = FindCommand(tokens[0].Unquoted);
		if (command is null)
		{
			return $"Unknown command '{tokens[0].Unquoted}'. Type /help for available commands.";
		}

		return $"Usage: {command.Usage} — {command.Description}";
	}

	public bool TryExecute(string input)
	{
		if (string.IsNullOrWhiteSpace(input))
		{
			return false;
		}

		var text = input.Trim();
		if (text.StartsWith("/", StringComparison.Ordinal))
		{
			return ExecuteCommand(text.Substring(1));
		}

		if (!_chat.TrySend(text))
		{
			AddLine("Message was not sent (session inactive or invalid text).", ConsoleLineKind.Error);
			return false;
		}

		return true;
	}

	public void Clear()
	{
		_lines.Clear();
		_log.LogDebug("[Command] console output cleared.");
	}

	public void Dispose()
	{
		_chat.MessageReceived -= OnChatLine;
		_session.SessionEnded -= OnSessionEnded;
	}

	private bool ExecuteCommand(string commandLine)
	{
		var tokens = Split(commandLine);
		if (tokens.Count == 0)
		{
			AddLine("Empty command.", ConsoleLineKind.Error);
			return false;
		}

		var command = FindCommand(tokens[0]);
		if (command is null)
		{
			AddLine($"Unknown command '{tokens[0]}'. Type /help for available commands.", ConsoleLineKind.Error);
			return false;
		}

		if (command.Permission == CommandPermission.HostOnly && _session.Role != SessionRole.Host)
		{
			AddLine("Permission denied: this command is host-only.", ConsoleLineKind.Error);
			return false;
		}

		try
		{
			var output = command.Handler(tokens);
			if (!string.IsNullOrWhiteSpace(output))
			{
				AddLine(output!, ConsoleLineKind.Success);
			}

			return true;
		}
		catch (Exception ex)
		{
			_log.LogError(ex, "[Command] {Name} threw while executing.", command.Name);
			AddLine($"Command error: {ex.Message}", ConsoleLineKind.Error);
			return true;
		}
	}

	[ConsoleCommand("help", "List available commands or show one command's usage.", CommandPermission.Anyone, "/help [command]", CommandArgumentKind.CommandName)]
	private string Help(IReadOnlyList<string> args)
	{
		if (args.Count < 2)
		{
			return HelpText();
		}

		var command = FindCommand(args[1]);
		return command is null
			? $"Unknown command '{args[1]}'. Type /help for available commands."
			: $"/{command.Name} — {command.Description}\nUsage: {command.Usage}";
	}

	[ConsoleCommand("clear", "Clear the console output.", CommandPermission.Anyone, "/clear")]
	private string ClearCommand(IReadOnlyList<string> _)
	{
		Clear();
		return string.Empty;
	}

	[ConsoleCommand("players", "List current session members.", CommandPermission.Anyone, "/players")]
	private string Players(IReadOnlyList<string> _)
	{
		var builder = new StringBuilder();
		builder.Append("Local: ").Append(_session.LocalSteamId).Append(" (").Append(_session.Role).Append(')');
		foreach (var member in _session.Members)
		{
			builder.Append('\n');
			builder.Append(member.SteamId);
			if (!string.IsNullOrWhiteSpace(member.DisplayName))
			{
				builder.Append(" [").Append(member.DisplayName).Append(']');
			}

			builder.Append(" handshake=").Append(member.Handshaken);
			builder.Append(" inWorld=").Append(member.InWorld);
			builder.Append(" rtt=").AppendFormat(CultureInfo.InvariantCulture, "{0:F0}", member.RttMs);
		}

		return builder.ToString();
	}

	[ConsoleCommand("rtt", "Show the last measured round-trip time.", CommandPermission.Anyone, "/rtt")]
	private string Rtt(IReadOnlyList<string> _)
	{
		return _session.LastRttMs < 0f
			? "No ping measured yet."
			: $"Last RTT: {_session.LastRttMs:F0} ms";
	}

	[ConsoleCommand("whoami", "Show local role, SteamId and session state.", CommandPermission.Anyone, "/whoami")]
	private string WhoAmI(IReadOnlyList<string> _) =>
		$"Role={_session.Role} SteamId={_session.LocalSteamId} Host={_session.HostSteamId} SessionActive={_session.SessionActive}";

	[ConsoleCommand("heal", "Use a carried medical item on the selected player(s).", CommandPermission.Anyone, "/heal <selector>", CommandArgumentKind.Selector)]
	private string Heal(IReadOnlyList<string> args)
	{
		if (args.Count < 2)
		{
			return "Usage: /heal <selector>";
		}

		var targets = ResolveSelector(args[1]);
		if (targets.Count == 0)
		{
			return $"No players match selector '{args[1]}'.";
		}

		foreach (var steamId in targets)
		{
			_playerInteraction.SendHealRequest(steamId);
		}

		var targetText = string.Join(", ", targets.Select(t => t.ToString(CultureInfo.InvariantCulture)));
		_log.LogInformation("[Command] /heal resolved {Selector} to {Count} player(s): {Targets}.",
			args[1], targets.Count, targetText);
		return $"Sent heal request to {targets.Count} player(s): {targetText}.";
	}

	private IReadOnlyList<ulong> ResolveSelector(string selector)
	{
		var targets = new List<CommandSelectorResolver.Target>
		{
			new(_entities.LocalPlayer.SteamId, true, _entities.LocalPlayer.Position, GetDisplayName(_entities.LocalPlayer.SteamId)),
		};
		var remotePlayers = _entities.RemotePlayers;
		for (var i = 0; i < remotePlayers.Count; i++)
		{
			var remote = remotePlayers[i];
			targets.Add(new(remote.SteamId, false, remote.Position, GetDisplayName(remote.SteamId)));
		}

		return CommandSelectorResolver.Resolve(selector, targets);
	}

	private string? GetDisplayName(ulong steamId) =>
		_session.Members.FirstOrDefault(m => m.SteamId == steamId)?.DisplayName;

	[ConsoleCommand("hostrules", "Host only: update host rules from a JSON object.", CommandPermission.HostOnly, "/hostrules <json>", CommandArgumentKind.Json)]
	private string HostRules(IReadOnlyList<string> args)
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
	private string Kick(IReadOnlyList<string> args)
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
	private string Ban(IReadOnlyList<string> args)
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
	private string Unban(IReadOnlyList<string> args)
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

	private void OnChatLine(ChatLine line) => AddLine(FormatChatLine(line), ConsoleLineKind.Info);

	private string FormatChatLine(ChatLine line)
	{
		if (line.SenderSteamId == _session.LocalSteamId)
		{
			return $"You: {line.Text}";
		}

		foreach (var member in _session.Members)
		{
			if (member.SteamId == line.SenderSteamId && !string.IsNullOrWhiteSpace(member.DisplayName))
			{
				return $"{member.DisplayName}: {line.Text}";
			}
		}

		return $"player-{line.SenderSteamId:X}: {line.Text}";
	}

	private CommandDefinition? FindCommand(string name)
	{
		if (name.StartsWith("/", StringComparison.Ordinal))
		{
			name = name.Substring(1);
		}

		return _commands.Find(name);
	}

	private static bool IsCommandTokenAtEnd(string input, CommandLineTokenizer.Token token)
	{
		var current = CommandLineTokenizer.CurrentToken(input);
		return current.Length == token.Length && current.Start == token.Start;
	}

	private static readonly CommandSuggestion[] JsonSuggestions =
	[
		new("{}", "Empty JSON object"),
		new("{\"key\": \"value\"}", "JSON object template"),
	];

	private IReadOnlyList<CommandSuggestion> SuggestFor(CommandArgumentKind kind, string prefix) => kind switch
	{
		CommandArgumentKind.CommandName => SuggestCommandNames(prefix),
		CommandArgumentKind.PlayerOrSteamId => SuggestMembers(prefix),
		CommandArgumentKind.SteamId => SuggestSteamIds(prefix),
		CommandArgumentKind.Selector => CommandSelectorSuggestions.Suggest(prefix),
		CommandArgumentKind.Json => [.. JsonSuggestions.Where(s => s.Text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))],
		CommandArgumentKind.ResourceLocation => ConsoleResourceLocationCatalog.Suggest(prefix),
		_ => [],
	};

	private IReadOnlyList<CommandSuggestion> SuggestCommandNames(string prefix) =>
		[.. _commands.All
			.Where(c => c.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
			.Select(c => new CommandSuggestion(c.Name, c.Description))
			.OrderBy(c => c.Text, StringComparer.OrdinalIgnoreCase)];

	private IReadOnlyList<CommandSuggestion> SuggestMembers(string prefix)
	{
		var result = new List<CommandSuggestion>();
		foreach (var member in _session.Members)
		{
			if (!string.IsNullOrWhiteSpace(member.DisplayName)
				&& member.DisplayName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
			{
				result.Add(new CommandSuggestion(member.DisplayName, "Session member display name"));
			}

			var decimalId = member.SteamId.ToString(CultureInfo.InvariantCulture);
			if (decimalId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
			{
				result.Add(new CommandSuggestion(decimalId, "SteamId"));
			}

			var hexId = "0x" + member.SteamId.ToString("X", CultureInfo.InvariantCulture);
			if (hexId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
			{
				result.Add(new CommandSuggestion(hexId, "SteamId (hex)"));
			}
		}

		return result;
	}

	private IReadOnlyList<CommandSuggestion> SuggestSteamIds(string prefix)
	{
		var result = new List<CommandSuggestion>();
		foreach (var steamId in _hostBans.BannedSteamIds)
		{
			var decimalId = steamId.ToString(CultureInfo.InvariantCulture);
			if (decimalId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
			{
				result.Add(new CommandSuggestion(decimalId, "Banned SteamId"));
			}

			var hexId = "0x" + steamId.ToString("X", CultureInfo.InvariantCulture);
			if (hexId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
			{
				result.Add(new CommandSuggestion(hexId, "Banned SteamId (hex)"));
			}
		}

		return result;
	}

	private void OnSessionEnded()
	{
		_lines.Clear();
		AddLine("Session ended.", ConsoleLineKind.Info);
	}

	private void AddLine(string text, ConsoleLineKind kind)
	{
		_lines.Add(new ConsoleLine(kind, text, _time.UtcNowTicks));
		if (_lines.Count > MaxLines)
		{
			_lines.RemoveAt(0);
		}
	}

	private static IReadOnlyList<string> Split(string text) =>
		[.. CommandLineTokenizer.Tokenize(text).Select(t => t.Unquoted)];

	private string HelpText()
	{
		var names = new List<string>(_commands.All.Count);
		foreach (var command in _commands.All)
		{
			names.Add($"/{command.Name} — {command.Description}");
		}

		return string.Join("\n", names);
	}
}
