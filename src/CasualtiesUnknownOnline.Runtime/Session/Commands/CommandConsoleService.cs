using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Application.Kernel;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Localization;
using CasualtiesUnknownOnline.Runtime.Session.Chat;
using CasualtiesUnknownOnline.Runtime.Session.Content;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.HostRules;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.Permissions;
using CasualtiesUnknownOnline.Runtime.Session.Persistence;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using CasualtiesUnknownOnline.Runtime.Session.World;
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
public sealed class CommandConsoleService : ICommandControl, ICommandCompletionSource, ICommandArgumentSuggestions, ISessionReset, IConsoleLineSink
{
	private const int MaxLines = 200;

	private readonly IChatControl _chat;
	private readonly ISessionControl _session;
	private readonly IPlayerInteractionControl _playerInteraction;
	private readonly ITimeSource _time;
	private readonly ILogger<CommandConsoleService> _log;
	private readonly ConsoleCommandRelay _consoleRelay;
	private readonly ConsoleReportWriter _reports;
	private readonly CheatPermissions _cheatRoster;
	private readonly CheatVerbGate _cheatVerbs;
	private readonly IHostRules _hostRuleValues;
	private readonly List<ConsoleLine> _lines = [];
	private readonly ConsoleCommandRegistry _commands;
	private readonly CommandConsoleCompletion _completion;
	private readonly ConsoleCommandAuthority _authority;
	private readonly IWorldSaveControl _saves;
	private readonly WorldRestoreAudit _restoreAudit;
	private readonly IStartingSupplyControl _startingSupplies;
	private readonly ItemStateSettlement _itemStateSettlement;
	private readonly IWorldSummonControl _worldSummon;
	private readonly IHealResetControl _healReset;
	private readonly AnnouncedHostRules _hostRules;
	private readonly IHostRuleChangeControl _hostRuleChanges;
	private readonly ILocalizationService? _localization;

	public CommandConsoleService(
		IChatControl chat,
		ISessionControl session,
		IHostBanService hostBans,
		IPlayerInteractionControl playerInteraction,
		IEntitySyncControl entities,
		IKernelItemFacts kernelItems,
		IHostRules hostRuleSurface,
		IHostRuleChangeControl hostRuleChanges,
		ITimeSource time,
		ILogger<CommandConsoleService> log,
		ConsoleCommandRegistry commandRegistry,
		IResourceLocationCatalog resourceLocations,
		IWorldSaveControl worldSaves,
		WorldRestoreAudit restoreAudit,
		IStartingSupplyControl startingSupplies,
		ILocalItemAccess localItems,
		IItemControl itemControl,
		IItemStateLanguage itemStateLanguage,
		ItemStateSettlement itemStateSettlement,
		IWorldSummonControl worldSummon,
		IHealResetControl healReset,
		AnnouncedHostRules hostRules,
		AnnouncedCheatPermissions cheatPermissions,
		CheatPermissions cheatRoster,
		ICheatPermissionAnnouncement cheatAnnouncement,
		IWorldControl world,
		CommandRelayHub relayHub,
		ISpectatorControl? spectator = null,
		ILocalizationService? localization = null)
	{
		_chat = chat;
		_session = session;
		// The member↔host seam: this side answers what it can run and prints what comes back; the mod domain owns the frames.
		_consoleRelay = new ConsoleCommandRelay(relayHub, commandRegistry, this, RefuseCheatFor, log);
		_reports = new ConsoleReportWriter(this);
		relayHub.RunOnHost = _consoleRelay.AnswerOnHost;
		relayHub.Arrived = (_, outcome) => _consoleRelay.Print(outcome);
		_playerInteraction = playerInteraction;
		_time = time;
		_log = log;
		_commands = commandRegistry;
		_saves = worldSaves;
		_restoreAudit = restoreAudit;
		_startingSupplies = startingSupplies;
		_itemStateSettlement = itemStateSettlement;
		_worldSummon = worldSummon;
		_healReset = healReset;
		_hostRules = hostRules;
		_hostRuleChanges = hostRuleChanges;
		_cheatRoster = cheatRoster;
		_cheatVerbs = new CheatVerbGate(session, _hostRules, cheatPermissions, log);
		_hostRuleValues = hostRuleSurface;
		_localization = localization;
		_chat.MessageReceived += OnChatLine;
		_session.SessionEnded += ResetSessionState;
		// The console is the player's surface for the save system's three answers: the
		// cut the frame-end seam resolved (long after /save returned), the Continue
		// click's own account, and the live-world half of a restore. All three are
		// subscribable state, so the console renders them where the player is already
		// reading — no polling and no second output buffer.
		_reports.Attach(_saves, _restoreAudit, _startingSupplies, _itemStateSettlement, _playerInteraction, _worldSummon, _healReset, _hostRuleChanges);
		// ...and for the one answer S4.3 added to the same story: what a player entering
		// a world the world has no character for was handed (decision 179's rule — one
		// notification, the account behind it).

		// ...and the write verb's own answer, which cannot be the command's return
		// value: the host judges a member's console write after the fact, so the
		// verdict reaches the operator here, where they typed the command.

		// ...and the answer to a member's /give, which the host authors: without this
		// subscription a granted give would print nothing on the member's screen and a
		// refused one would look like a line that did nothing.

		// ...and the two answers the heal cheat verb prints where the player reads: the host's own
		// sentence to a member's request, and the notice of a reset the host applied to THIS client's
		// body (a member's reset arrives as an instruction, never as its own command's return).

		// ...and the host's answer to a member's /gamerule, whose config write is the host's (never this client's).

		_commands.AddBuiltIns(this);
		_commands.AddBuiltIns(new HostAdminCommands(hostBans, session));
		_commands.AddBuiltIns(new CheatAdminCommands(session, cheatRoster, cheatAnnouncement, log));
		_commands.AddBuiltIns(new SpectatorCommands(session, entities, spectator, log));
		_commands.AddBuiltIns(new GamemodeCommands(spectator, log));
		_commands.AddBuiltIns(new WorldSaveCommands(worldSaves, log));
		// The item-data family's two verb groups share one gate and one target
		// resolver: /data writes this client's own item, /give makes a new one, and
		// both answer the same cheat rulings and the same selector verdict.
		_authority = new ConsoleCommandAuthority(session, _cheatVerbs);
		// The host-rule verb is the cheat family's too (the user's 2026-10-24 ruling); its own group holds the gate.
		_commands.AddBuiltIns(new HostRuleCommands(hostRuleSurface, hostRuleChanges, _cheatVerbs, log));
		var targets = new ItemDataTargets(kernelItems, session, entities, localItems);
		var writes = new ItemDataWriteCommands(kernelItems, session, localItems, itemControl, itemStateLanguage, log, _cheatVerbs, targets);
		_commands.AddBuiltIns(new ItemDataCommands(kernelItems, itemStateLanguage, log, targets, writes));
		_commands.AddBuiltIns(new ItemGiveCommands(_playerInteraction, _cheatVerbs, targets, log));
		_commands.AddBuiltIns(new WorldSummonCommands(worldSummon, _cheatVerbs));
		_commands.AddBuiltIns(new HealResetCommands(healReset, _cheatVerbs, session, entities));
		// A run's own levers: the acceptance-only verbs must be reachable from the console, and their
		// owner registers itself through the same attribute scan as every other group above. The cheat
		// gate is the permission: a lever that can move a trader's credit is not a player's shortcut.
		_commands.AddBuiltIns(new AcceptanceCommands(session, world, _cheatVerbs));
		// The completion side is its own collaborator (this class is at the aggregate-size gate): it owns
		// the contextual sources and asks the same authority predicate the dispatcher and /help use.
		_completion = new CommandConsoleCompletion(
			_commands, session, hostBans, resourceLocations, _authority, _cheatVerbs,
			kernelItems, itemStateLanguage, targets, hostRuleSurface, log);
		AddKeyedLine("console.ready", "CUO command console ready. Type /help for available commands, or just type to chat.", ConsoleLineKind.Info);
	}

	public IReadOnlyList<ConsoleLine> Lines => _lines;

	/// <summary>
	/// The command metadata projection for the UI, filtered by the same authority the suggestions use: a
	/// surface that renders this list must not name a command the caller cannot run, or the list becomes the
	/// leak the completion filter closes. A hidden command typed in full still reaches its handler.
	/// </summary>
	public IReadOnlyList<CommandSpec> Commands => [.. _commands.All.Where(_authority.MayRun).Select(command => command.ToSpec())];

	public IReadOnlyList<CommandSuggestion> Suggest(string input) => _completion.Suggest(input);

	public IReadOnlyList<CommandSuggestion> Suggest(CommandArgumentKind kind, string prefix) => _completion.Suggest(kind, prefix);

	public CommandHint? GetHint(string input) => _completion.GetHint(input);

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
			AddKeyedLine("console.chat.not_sent", "Message was not sent (session inactive or invalid text).", ConsoleLineKind.Error);
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
		_reports.Detach();
	}

	/// <summary>The cut the frame-end seam resolved — only the cuts the player asked for (a layer advance or an interval autosave is logged, not printed).</summary>
	private bool ExecuteCommand(string commandLine)
	{
		var tokens = Split(commandLine);
		if (tokens.Count == 0)
		{
			AddKeyedLine("console.command.empty", "Empty command.", ConsoleLineKind.Error);
			return false;
		}

		var command = FindCommand(tokens[0]);
		if (command is null)
		{
			AddKeyedLine("console.command.unknown", $"Unknown command '{tokens[0]}'. Type /help for available commands.", ConsoleLineKind.Error, tokens[0]);
			return false;
		}

		// The declaration half of the authority is enforced here, before the handler runs. The cheat half
		// deliberately is NOT: a gated verb must reach its handler, which runs its own grammar and then the
		// gate, so the refusal it prints is the family's own (and typing a hidden command is still refused
		// by name rather than as unknown).
		if (command.Permission == CommandPermission.HostOnly && _session.Role != SessionRole.Host)
		{
			AddKeyedLine("console.command.host_only", ConsoleCommandAuthority.HostOnlyRefusal, ConsoleLineKind.Error);
			return false;
		}

		// The cheat family's two rulings, on the side that typed the line: a member whose host has the switch closed or who is not on the roster learns it here, before
		// anything is sent or applied (user direction 2026-10-30: a declared cheat command is gated, never silently run).
		if (command.Permission == CommandPermission.Cheat && _cheatVerbs.TryRefuse(command.Name, out var cheatRefusal))
		{
			PrintRefusal(cheatRefusal);
			return false;
		}

		// A member's HostRelayed command runs on the HOST: this line goes over the wire, and the answer prints here when it arrives (user direction 2026-10-30:
		// a declared non-local command is declared, gated and run by the host, never silently run locally).
		if (_consoleRelay.TrySend(command, tokens, _session.Role == SessionRole.Host))
		{
			return true;
		}

		try
		{
			// The handler says what its answer IS: a refusal prints in the error colour, so a verb that answers
			// "unknown rule" or "not permitted" reads as one instead of as a completed action.
			var answer = command.Answer(tokens);
			if (answer.HasText)
			{
				// An answer that names its catalogue key is printed in the client's own language; one that does not keeps its
				// sentence (the log, the fallback, and every answer assembled from free data).
				var kind = answer.Kind == ConsoleAnswerKind.Refusal ? ConsoleLineKind.Error : ConsoleLineKind.Success;
				if (answer.HasParts)
				{
					// One line per part, each rendered by THIS client's catalogue: a host's own answer never crosses the wire, so its parts are
					// the only way its lines reach the operator in the operator's language.
					foreach (var part in answer.Parts!)
					{
						var partArgs = new string[part.Args.Count];
						for (var i = 0; i < partArgs.Length; i++)
						{
							partArgs[i] = part.Args[i];
						}

						// The line keeps the ANSWER's own sentence as its fallback: a client whose catalogue has no entry for the part (a test host, an
						// older table) must print the sentence the command built, never the key.
						var text = _localization?.Format(part.Key, partArgs) ?? "";
						if (text.Length == 0 || string.Equals(text, part.Key, StringComparison.Ordinal))
						{
							text = answer.Text ?? part.Key;
						}

						AddKeyedLine(part.Key, text, kind, partArgs);
					}

					return true;
				}

				if (answer.Key is { } key)
				{
					// The line keeps the ANSWER's own sentence as its fallback, and the catalogue's sentence as its text when this client's table has
					// the key: one keyed answer behaves like one keyed PART, so the history and the host's log read what the player reads.
					var keyArgs = new string[answer.Args?.Count ?? 0];
					for (var i = 0; i < keyArgs.Length; i++)
					{
						keyArgs[i] = answer.Args![i];
					}

					var text = _localization?.Format(key, keyArgs) ?? "";
					if (text.Length == 0 || string.Equals(text, key, StringComparison.Ordinal))
					{
						text = answer.Text!;
					}

					AddKeyedLine(key, text, kind, keyArgs);
				}
				else
				{
					AddLine(answer.Text!, kind);
				}
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
	private ConsoleAnswer Help(IReadOnlyList<string> args)
	{
		if (args.Count < 2)
		{
			return ConsoleAnswer.Ok(HelpText());
		}

		var command = FindCommand(args[1]);
		if (command is null)
		{
			return ConsoleAnswer.Refused($"Unknown command '{args[1]}'. Type /help for available commands.", "console.command.unknown", args[1]);
		}

		// A command this caller may not run answers the refusal it would meet, not "unknown": the console
		// must not teach a word by help and then pretend it does not exist. The list above hides the same
		// commands, so a player who reads /help never learns one the console would refuse.
		return _authority.TryRefusal(command)
			?? ConsoleAnswer.Ok($"/{command.Name} — {CommandDescriptions.Of(_localization, command.Name, command.Description)}\nUsage: {command.Usage}", "console.help.command", command.Name, CommandDescriptions.Of(_localization, command.Name, command.Description), CommandDescriptions.UsageOf(_localization, command.Name, command.Usage));
	}

	/// <summary>One command's one-line description in the reader's language (see <see cref="CommandDescriptions"/>).</summary>


	[ConsoleCommand("clear", "Clear the console output.", CommandPermission.Anyone, "/clear")]
	private ConsoleAnswer ClearCommand(IReadOnlyList<string> _)
	{
		Clear();
		return ConsoleAnswer.Silent;
	}

	[ConsoleCommand("players", "List current session members.", CommandPermission.Anyone, "/players")]
	private ConsoleAnswer Players(IReadOnlyList<string> _)
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

		return ConsoleAnswer.Ok(builder.ToString());
	}

	[ConsoleCommand("rtt", "Show the last measured round-trip time.", CommandPermission.Anyone, "/rtt")]
	private ConsoleAnswer Rtt(IReadOnlyList<string> _)
	{
		return ConsoleAnswer.Ok(_session.LastRttMs < 0f
			? "No ping measured yet."
			: $"Last RTT: {_session.LastRttMs:F0} ms");
	}

	[ConsoleCommand("whoami", "Show local role, SteamId and session state.", CommandPermission.Anyone, "/whoami")]
	private ConsoleAnswer WhoAmI(IReadOnlyList<string> _) =>
		ConsoleAnswer.Ok($"Role={_session.Role} SteamId={_session.LocalSteamId} Host={_session.HostSteamId} SessionActive={_session.SessionActive}", "console.answer.session_facts", _session.Role.ToString(), _session.LocalSteamId.ToString(CultureInfo.InvariantCulture), _session.HostSteamId.ToString(CultureInfo.InvariantCulture), _session.SessionActive ? "true" : "false");

	// The membership commands (/kick, /ban, /unban) live in HostAdminCommands, the host-rule
	// verb in HostRuleCommands and the save commands (/save) in WorldSaveCommands: each group
	// is registered as its own owner, so a new command family never grows this class past the
	// architecture gate.

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

	/// <summary>
	/// The cheat family's judgement for ONE player: the host's switch and roster, asked about a member the host is acting for (a relayed command) or about the local
	/// player (the line typed here). Null means the family admits them; a refusal names its ground, so the operator learns whether to ask for the switch or a grant.
	/// </summary>
	private ConsoleAnswer? RefuseCheatFor(ulong member)
	{
		if (_session.Role == SessionRole.Host && member == _session.LocalSteamId)
		{
			return null;
		}

		if (!_session.SessionActive)
		{
			return ConsoleAnswer.Refused("No active session — a cheat verb needs one. Nothing was done here.", "cheat.refused.no_session");
		}

		if (!_hostRuleValues.AllowMemberCheatVerbs)
		{
			return ConsoleAnswer.Refused(
				"Refused: the host's cheat switch is off, so no member may run a cheat verb. Nothing was done here.",
				"cheat.refused.switch_closed",
				HostRuleId.AllowMemberCheatVerbs.ToString());
		}

		return _cheatRoster.IsGranted(member)
			? null
			: ConsoleAnswer.Refused(
				"Refused: the host's cheat roster does not name you, so this command cannot land. The host grants it with /op. Nothing was done here.",
				"cheat.refused.not_on_roster");
	}

	/// <summary>Prints a keyed refusal in the error colour: the cheat family's grounds reach the operator as the family's own sentences.</summary>
	private void PrintRefusal(ConsoleAnswer answer)
	{
		var args = new string[answer.Args?.Count ?? 0];
		for (var index = 0; index < args.Length; index++)
		{
			args[index] = answer.Args![index];
		}

		if (answer.Key is not { } key)
		{
			// An answer with no key is still printed: the cheat family always keys its grounds, and this branch keeps a future one from blanking the line.
			AddLine(answer.Text ?? string.Empty, ConsoleLineKind.Error, notifiable: true);
			return;
		}

		AddKeyedLine(key, answer.Text ?? string.Empty, ConsoleLineKind.Error, args);
	}

	private CommandDefinition? FindCommand(string name)
	{
		if (name.StartsWith("/", StringComparison.Ordinal))
		{
			name = name.Substring(1);
		}

		return _commands.Find(name);
	}

	public void ResetSessionState()
	{
		_lines.Clear();
		AddKeyedLine("console.session_ended", "Session ended.", ConsoleLineKind.Info);
	}

	private void AddLine(string text, ConsoleLineKind kind) => AddLine(text, kind, notifiable: true);

	/// <summary>
	/// A line whose sentence the CATALOGUE owns: the English text is still carried for the log and for a client whose language
	/// has no entry, and the key lets the UI print the same line in the client's own language. This is the funnel the console's
	/// player-facing prose moves through family by family (see `docs/backlog/todo/console-command-replies-bilingual.md`).
	/// </summary>
	/// <summary>
	/// Runs a relayed definition on the HOST for the member who asked: the declared permission is the first gate and the host's cheat roster the second, then the
	/// definition's own relay answer runs with that member as the requester (so its handler says "you" about the player who typed). A command that did not
	/// declare <c>HostRelayed</c> is refused here: the member should have run it locally.
	/// </summary>
	void IConsoleLineSink.AddKeyedLine(string key, string text, ConsoleLineKind kind, params string[] args) =>
		AddKeyedLine(key, text, kind, args);

	private void AddKeyedLine(string key, string text, ConsoleLineKind kind, params string[] args) =>
		_lines.Add(new ConsoleLine(kind, text, _time.UtcNowTicks, true, key, args.Length == 0 ? null : args));

	/// <summary>
	/// A line that belongs in the HISTORY only: the closed console does not announce
	/// it, because it is one item of a report whose headline already did (see
	/// <see cref="ConsoleLine.Notifiable"/>).
	/// </summary>
	private void AddHistoryLine(string text) => AddLine(text, ConsoleLineKind.Info, notifiable: false);

	void IConsoleLineSink.AddLine(string text, ConsoleLineKind kind, bool notifiable) => AddLine(text, kind, notifiable);

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
			if (_authority.MayRun(command))
			{
				names.Add($"/{command.Name} — {CommandDescriptions.Of(_localization, command.Name, command.Description)}");
			}
		}

		return string.Join("\n", names);
	}
}
