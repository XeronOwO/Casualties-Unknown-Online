using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Chat;
using CasualtiesUnknownOnline.Runtime.Session.Content;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.HostRules;
using CasualtiesUnknownOnline.Runtime.Session.Persistence;
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
public sealed class CommandConsoleService : ICommandControl, ICommandCompletionSource, ICommandArgumentSuggestions, ISessionReset
{
	private const int MaxLines = 200;

	private readonly IChatControl _chat;
	private readonly ISessionControl _session;
	private readonly IHostBanService _hostBans;
	private readonly IPlayerInteractionControl _playerInteraction;
	private readonly IEntitySyncControl _entities;
	private readonly ITimeSource _time;
	private readonly ILogger<CommandConsoleService> _log;
	private readonly List<ConsoleLine> _lines = [];
	private readonly ConsoleCommandRegistry _commands;
	private readonly IResourceLocationCatalog _resourceLocations;
	private readonly IWorldSaveControl _saves;
	private readonly WorldRestoreAudit _restoreAudit;
	private readonly IStartingSupplyControl _startingSupplies;

	public CommandConsoleService(
		IChatControl chat,
		ISessionControl session,
		IHostBanService hostBans,
		IPlayerInteractionControl playerInteraction,
		IEntitySyncControl entities,
		IHostRulesEditor hostRulesEditor,
		ITimeSource time,
		ILogger<CommandConsoleService> log,
		ConsoleCommandRegistry commandRegistry,
		IResourceLocationCatalog resourceLocations,
		IWorldSaveControl worldSaves,
		WorldRestoreAudit restoreAudit,
		IStartingSupplyControl startingSupplies)
	{
		_chat = chat;
		_session = session;
		_hostBans = hostBans;
		_playerInteraction = playerInteraction;
		_entities = entities;
		_time = time;
		_log = log;
		_commands = commandRegistry;
		_resourceLocations = resourceLocations;
		_saves = worldSaves;
		_restoreAudit = restoreAudit;
		_startingSupplies = startingSupplies;
		_chat.MessageReceived += OnChatLine;
		_session.SessionEnded += ResetSessionState;
		// The console is the player's surface for the save system's three answers: the
		// cut the frame-end seam resolved (long after /save returned), the Continue
		// click's own account, and the live-world half of a restore. All three are
		// subscribable state, so the console renders them where the player is already
		// reading — no polling and no second output buffer.
		_saves.CutReported += OnCutReported;
		_saves.RestoreReported += OnRestoreReported;
		_restoreAudit.Reported += OnRestoreLiveWrite;
		// ...and for the one answer S4.3 added to the same story: what a player entering
		// a world the world has no character for was handed (decision 179's rule — one
		// notification, the account behind it).
		_startingSupplies.Reported += OnStartingSuppliesReported;
		_commands.AddBuiltIns(this);
		_commands.AddBuiltIns(new HostAdminCommands(hostBans, session, hostRulesEditor, log));
		_commands.AddBuiltIns(new WorldSaveCommands(worldSaves, log));
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
		_session.SessionEnded -= ResetSessionState;
		_saves.CutReported -= OnCutReported;
		_saves.RestoreReported -= OnRestoreReported;
		_restoreAudit.Reported -= OnRestoreLiveWrite;
		_startingSupplies.Reported -= OnStartingSuppliesReported;
	}

	/// <summary>The cut the frame-end seam resolved — only the cuts the player asked for (a layer advance or an interval autosave is logged, not printed).</summary>
	private void OnCutReported(WorldCutReport report)
	{
		if (report.PlayerInitiated)
		{
			AddLine(report.Describe(), report.Captured ? ConsoleLineKind.Success : ConsoleLineKind.Error);
		}
	}

	/// <summary>
	/// The Continue click's account (§6: no silent loss). The DISPOSITION becomes the
	/// one notification the player is interrupted by, and the account itself — the
	/// one-line summary with its per-domain counts, then one line per skipped content
	/// id, refused claimant, unbindable native field and backup fallback — goes into
	/// the history behind it, where it can be read without pushing the player's other
	/// notices out of the closed console's newest-few window.
	/// </summary>
	private void OnRestoreReported(WorldRestoreReport report)
	{
		var headline = report.Result switch
		{
			WorldRestoreReport.Disposition.Refused => $"CUO continue refused: {report.Summary}",
			WorldRestoreReport.Disposition.Abandoned => $"CUO continue abandoned: {report.Summary}",
			_ when report.Clean => $"CUO restored world {report.WorldId}: nothing was lost.",
			_ => $"CUO restored world {report.WorldId} with {report.Details.Count} damaged item(s); the console history names them.",
		};
		AddLine(headline, report.Clean ? ConsoleLineKind.Success : ConsoleLineKind.Error);

		// The refusal and abandonment headlines ARE the account's one line; the applied
		// ones are a short form of it, so only those add the summary to the history.
		if (report.Result == WorldRestoreReport.Disposition.Applied)
		{
			AddHistoryLine(report.Summary);
		}

		foreach (var detail in report.Details)
		{
			AddHistoryLine(detail);
		}
	}

	/// <summary>The live-world half of a restore: the world-entry seam wrote — or could not write — the restored facts.</summary>
	private void OnRestoreLiveWrite(WorldRestoreLiveWriteReport report) =>
		AddLine($"CUO restore of world {report.WorldId}: {report.Summary}", report.Complete ? ConsoleLineKind.Success : ConsoleLineKind.Error);

	/// <summary>
	/// The starting-supplies grant of a player this world had no character for (S4.3):
	/// ONE line, the same words the log carries, so the player's screen and the log can
	/// be compared without translating. The three dispositions are one event each and the
	/// account is one line long by construction, so nothing goes into the history behind
	/// it — a granted report names its items in that same line, and an empty grant
	/// (disabled, or already owned through the game's own first-layer grant) has nothing
	/// behind it to read.
	///
	/// An incomplete grant is the one failure shape: the line says so (it names what
	/// stayed on the ground), and it is announced as an error rather than a success —
	/// a player who cannot find a given item must not have to read the sentence twice
	/// to learn it was never placed.
	/// </summary>
	private void OnStartingSuppliesReported(StartingSupplyGrantReport report) =>
		AddLine(
			report.Describe(),
			report.Outcome == StartingSupplyGrantReport.Disposition.Granted && report.Complete
				? ConsoleLineKind.Success
				: report.Outcome == StartingSupplyGrantReport.Disposition.Granted
					? ConsoleLineKind.Error
					: ConsoleLineKind.Info);

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

	// The host-administration commands (/hostrules, /kick, /ban, /unban) live in
	// HostAdminCommands, and the save commands (/save) in WorldSaveCommands: each
	// group is registered as its own owner, so a new command family never grows
	// this class past the architecture gate.

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
		CommandArgumentKind.ResourceLocation => SuggestResourceLocations(prefix),
		_ => [],
	};

	/// <summary>
	/// Resource/id completion from the content-id catalog: the user may type a
	/// canonical id prefix, the bare content id, or the localized display name,
	/// but every suggestion is the canonical <c>namespace:path</c> id (the
	/// catalog owns matching/ranking; this method only projects it for the UI).
	/// </summary>
	private IReadOnlyList<CommandSuggestion> SuggestResourceLocations(string prefix) =>
		[.. _resourceLocations.Suggest(prefix)
			.Select(entry => new CommandSuggestion(entry.Id.ToString(), $"{entry.Kind} · {entry.DisplayName}"))];

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

	public void ResetSessionState()
	{
		_lines.Clear();
		AddLine("Session ended.", ConsoleLineKind.Info);
	}

	private void AddLine(string text, ConsoleLineKind kind) => AddLine(text, kind, notifiable: true);

	/// <summary>
	/// A line that belongs in the HISTORY only: the closed console does not announce
	/// it, because it is one item of a report whose headline already did (see
	/// <see cref="ConsoleLine.Notifiable"/>).
	/// </summary>
	private void AddHistoryLine(string text) => AddLine(text, ConsoleLineKind.Info, notifiable: false);

	private void AddLine(string text, ConsoleLineKind kind, bool notifiable)
	{
		_lines.Add(new ConsoleLine(kind, text, _time.UtcNowTicks, notifiable));
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
