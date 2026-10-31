using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Application.Kernel;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Session.Content;
using CasualtiesUnknownOnline.Runtime.Session.HostRules;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The console's completion side: which commands are offered, what the token under the caret completes to,
/// and the usage hint. It was split out of <see cref="CommandConsoleService"/> (which was at the
/// class-size gate) along the responsibility line the console already had — this class asks what the
/// registry and the session HOLD, while the service scripts input, gates permissions and renders answers.
///
/// <para>
/// One authority predicate filters everything here: <see cref="ConsoleCommandAuthority"/>. A command the
/// caller may not run is not offered by name, its arguments do not complete when the line is typed anyway,
/// and <c>/help</c> uses the same predicate. Hiding is a courtesy, not the enforcement — the handler still
/// refuses the typed line.
/// </para>
///
/// <para>
/// Contextual sources run before the per-argument-kind fallback, in a fixed order: the item-data verb word,
/// the host-rule vocabulary, then the SNBT state (which needs a brace and the target already typed). The
/// cheat gate is asked once per suggestion call for the item-data write half, in its QUIET form, so a
/// keystroke never writes a refusal line to the log.
/// </para>
/// </summary>
internal sealed class CommandConsoleCompletion(
	ConsoleCommandRegistry commands,
	ISessionControl session,
	IHostBanService bans,
	IResourceLocationCatalog resourceLocations,
	ConsoleCommandAuthority authority,
	CheatVerbGate cheatVerbs,
	IKernelItemFacts kernelItems,
	IItemStateLanguage itemStateLanguage,
	ItemDataTargets targets,
	IHostRules hostRules,
	ILogger log)
{
	private static readonly CommandSuggestion[] JsonSuggestions =
	[
		new("{}", CommandSuggestionHint.JsonEmpty),
		new("{\"key\": \"value\"}", CommandSuggestionHint.JsonTemplate),
	];

	private readonly ConsoleCommandRegistry _commands = commands;
	private readonly ISessionControl _session = session;
	private readonly IHostBanService _bans = bans;
	private readonly IResourceLocationCatalog _resourceLocations = resourceLocations;
	private readonly ConsoleCommandAuthority _authority = authority;
	private readonly CheatVerbGate _cheatVerbs = cheatVerbs;
	private readonly ItemStateCompletion _itemState = new(kernelItems, itemStateLanguage, targets);
	private readonly HostRuleCompletion _hostRules = new(hostRules);
	private readonly ILogger _log = log;

	internal IReadOnlyList<CommandSuggestion> Suggest(string input)
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

			// A command is offered by its NAME and by every ALIAS it declares (user direction 2026-10-30): an alias reaches the same handler, so a list that
			// hid it would teach the player fewer words than the console accepts.
			return
			[
				.. _commands.All
					.Where(_authority.MayRun)
					.SelectMany(command => command.Aliases.Prepend(command.Name).Select(word => (Word: word, command.Name)))
					.Where(candidate => candidate.Word.StartsWith(partial, StringComparison.OrdinalIgnoreCase))
					.Select(candidate => new CommandSuggestion(candidate.Word, CommandSuggestionHint.Command, Args: [candidate.Name]))
					.OrderBy(suggestion => suggestion.Text, StringComparer.OrdinalIgnoreCase)
			];
		}

		if (!isCommand)
		{
			return [];
		}

		var command = FindCommand(tokens[0].Unquoted);
		if (command is null || !_authority.MayRun(command))
		{
			return [];
		}

		var current = CommandLineTokenizer.CurrentToken(input);
		var isData = string.Equals(command.Name, "data", StringComparison.OrdinalIgnoreCase);
		var mayRunCheatVerbs = isData && _cheatVerbs.MayRun("data");
		if (ItemDataVerbCompletion.TrySuggest(command.Name, tokens, current, mayRunCheatVerbs, out var dataVerbs))
		{
			return dataVerbs;
		}

		if (_hostRules.TrySuggest(command.Name, tokens, current, out var ruleSuggestions))
		{
			return ruleSuggestions;
		}

		// A verb whose argument is a closed word set answers here, from the same values its handler accepts.
		if (GamemodeCompletion.TrySuggest(command.Name, tokens, current, out var modeSuggestions))
		{
			return modeSuggestions;
		}

		// The write half's SNBT state is the same cheat verb's argument: a caller the gate refuses gets no
		// state vocabulary either, while /data get's own completion is unaffected.
		var dataWriteLocked = isData
			&& tokens.Count > 1
			&& ItemDataVerbParser.IsWriteVerb(tokens[1].Unquoted)
			&& !mayRunCheatVerbs;

		// The state comes BEFORE the verb's own word list (user direction 2026-10-30): after a resolved item id the grammar expects the component block, and the
		// list must carry it — a give also accepts `slot` there, so the two sources are MERGED instead of one winning the keystroke.
		if (!dataWriteLocked && _itemState.TrySuggest(command.Name, tokens, current, out var stateSuggestions) && stateSuggestions.Count > 0)
		{
			return ArgumentWordCompletion.TrySuggest(command.Name, tokens, current, out var alsoWords)
				? [.. stateSuggestions, .. alsoWords]
				: stateSuggestions;
		}

		// …and so does an argument whose legal words come from the parser the verb reads (a summon position, a give slot).
		if (ArgumentWordCompletion.TrySuggest(command.Name, tokens, current, out var wordSuggestions))
		{
			return wordSuggestions;
		}

		var argumentIndex = current.Length == 0 ? tokens.Count : tokens.Count - 1;
		if (argumentIndex < 1)
		{
			return [];
		}

		// A DEFINITION'S OWN COMPLETER IS ASKED FIRST (user direction 2026-10-30): the mod's parameter knows its words and may depend on the arguments already
		// typed, which no per-kind source can express. The framework still owns the list's behaviour, so a completer only proposes; a throw or a hang is
		// isolated here and reads as "this argument offered nothing" instead of taking the overlay down.
		if (FromTheParameter(command, tokens, argumentIndex, current) is { } fromParameter)
		{
			return fromParameter;
		}

		if (argumentIndex > command.ArgumentKinds.Count)
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
		return SuggestFor(kind.Value, prefix, command);
	}

	/// <summary>One argument kind's candidates, the narrow surface the UI can also ask directly.</summary>
	internal IReadOnlyList<CommandSuggestion> Suggest(CommandArgumentKind kind, string prefix) => SuggestFor(kind, prefix, null);

	/// <summary>
	/// The hint about the line as typed, or null when there is nothing to explain. A hint is not a completion: rows offer what COULD be typed, this explains what WAS
	/// typed while the player can still fix it (user direction 2026-10-30: the console used to stay silent until Enter, and a refused token only got an answer after
	/// the line had been sent). Two cases are worth a line — an unknown verb, and a token nothing matches at a position the grammar expects something in; a line the
	/// list already explains stays quiet, so the hint never competes with the candidates.
	/// </summary>
	internal CommandHint? GetHint(string input)
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

		// A lone "/" is the console just opened, not a wrong command: nothing has been typed yet.
		if (string.Equals(tokens[0].Unquoted, "/", StringComparison.Ordinal))
		{
			return null;
		}

		var command = FindCommand(tokens[0].Unquoted);
		if (command is null)
		{
			return new CommandHint("console.hint.unknown_command", tokens[0].Unquoted);
		}

		// A verb typed out WHOLE and nothing after it names what it accepts; a partially typed name does not, because the list already explains that keystroke.
		var current = CommandLineTokenizer.CurrentToken(input);
		if (tokens.Count == 1 && current.Length > 0)
		{
			return new CommandHint("console.hint.usage", command.Usage);
		}

		if (tokens.Count <= 1)
		{
			return null;
		}

		if (current.Length > 0 && tokens.Count > 1 && Suggest(input).Count == 0 && !IsCompleteState(command, tokens, input))
		{
			return new CommandHint("console.hint.no_match", current.Unquoted, command.Usage);
		}

		return null;
	}

	/// <summary>
	/// Whether the line already names something the verb accepts in full, so "nothing matches" would be a false alarm: an item state typed out, for instance, has no
	/// candidates left but is exactly what the grammar wants.
	/// </summary>
	private static bool IsCompleteState(CommandDefinition command, IReadOnlyList<CommandLineTokenizer.Token> tokens, string input)
	{
		var current = CommandLineTokenizer.CurrentToken(input);
		return current.Unquoted.IndexOf('{') >= 0 && current.Unquoted.TrimEnd().EndsWith("}");
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

	private IReadOnlyList<CommandSuggestion> SuggestFor(CommandArgumentKind kind, string prefix, CommandDefinition? command) => kind switch
	{
		// The one argument kind that names commands is /help's: completing a hidden name there would leak the
		// word the list just hid, so the authority filter applies to it too.
		CommandArgumentKind.CommandName => CommandSuggestions.CommandNames([.. _commands.All.Where(_authority.MayRun)], prefix),
		CommandArgumentKind.PlayerOrSteamId => CommandSuggestions.Members(_session, prefix),
		CommandArgumentKind.SteamId => CommandSuggestions.BannedSteamIds(_bans, prefix),
		CommandArgumentKind.Selector => CommandSelectorSuggestions.Suggest(prefix),
		CommandArgumentKind.Json => [.. JsonSuggestions.Where(s => s.Text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))],
		CommandArgumentKind.ResourceLocation => SuggestResourceLocations(prefix),
		_ => [],
	};

	/// <summary>
	/// Resource/id completion from the content-id catalog: the user may type a canonical id prefix, the bare content id, or the localized display name, but every
	/// suggestion is the CANONICAL <c>namespace:path</c> — the form `/give` and `/summon` now resolve back to the game's own id, so the list teaches exactly the
	/// line the verb accepts (user direction 2026-10-30). The catalog owns matching and ranking; this method only projects it for the UI.
	/// </summary>
	private IReadOnlyList<CommandSuggestion> SuggestResourceLocations(string prefix) =>
		[.. _resourceLocations.Suggest(prefix)
			.Select(entry => new CommandSuggestion(
				entry.Id.ToString(),
				CommandSuggestionHint.ResourceLocation, Args: [entry.Kind, entry.DisplayName]))];

	/// <summary>
	/// The parameter's own completer, when the command is a definition that declares one for the argument under the cursor. Returns <c>null</c> when the
	/// command has no definition, the parameter has no completer, or the completer could not answer — so the caller falls back to the framework's own sources.
	/// </summary>
	private IReadOnlyList<CommandSuggestion>? FromTheParameter(
		CommandDefinition command,
		IReadOnlyList<CommandLineTokenizer.Token> tokens,
		int argumentIndex,
		CommandLineTokenizer.Token current)
	{
		if (command.Contract is not { } contract)
		{
			return null;
		}

		var index = argumentIndex - 1;
		if (index < 0 || index >= contract.Parameters.Count)
		{
			return null;
		}

		if (contract.Parameters[index].Completer is not { } completer)
		{
			return null;
		}

		IReadOnlyList<string> previous = tokens.Count > 1
			? [.. tokens.Skip(1).Take(index).Select(token => token.Unquoted)]
			: [];
		var context = new ParameterCompletionContext(
			contract.Name,
			index,
			previous,
			current.Length == 0 ? string.Empty : current.Unquoted,
			ModSessionSnapshot.Capture(_session));
		IReadOnlyList<CommandCompletion> candidates;
		try
		{
			candidates = completer.Complete(context) ?? [];
		}
		catch (Exception exception)
		{
			// A mod's completer runs inside a frame; a throw must not take the overlay down, and a silent empty list would hide the cause.
			_log.LogWarning(
				"[Console] /{Command} parameter {Index}'s completer threw {Exception}: the argument offers nothing this keystroke.",
				contract.Name, index, exception.GetType().Name);
			return [];
		}

		return [.. candidates.Select(candidate => ToSuggestion(candidate, contract.Parameters[index].HintKey))];
	}

	/// <summary>One proposed candidate, as the list draws it: its text, its insertion rule, and the hint the row shows (the parameter's key when it has one).</summary>
	private static CommandSuggestion ToSuggestion(CommandCompletion candidate, string? parameterHintKey)
	{
		if (!string.IsNullOrEmpty(candidate.HintKey))
		{
			return new CommandSuggestion(
				candidate.Text,
				CommandSuggestionHint.KeyedText,
				candidate.Verbatim,
				[candidate.HintKey!, .. candidate.HintArgs]);
		}

		if (!string.IsNullOrEmpty(parameterHintKey))
		{
			return new CommandSuggestion(candidate.Text, CommandSuggestionHint.KeyedText, candidate.Verbatim, [parameterHintKey!]);
		}

		return new CommandSuggestion(candidate.Text, CommandSuggestionHint.None, candidate.Verbatim);
	}

	/// <summary>The context one parameter's completer reads: the command, which parameter, the arguments before it, the partial token and the session.</summary>
	private sealed class ParameterCompletionContext(
		string commandName,
		int parameterIndex,
		IReadOnlyList<string> arguments,
		string partial,
		ISessionInfo session) : ICompletionContext
	{
		public string CommandName { get; } = commandName;

		public int ParameterIndex { get; } = parameterIndex;

		public IReadOnlyList<string> Arguments { get; } = arguments;

		public string Partial { get; } = partial;

		public ISessionInfo Session { get; } = session;
	}
}
