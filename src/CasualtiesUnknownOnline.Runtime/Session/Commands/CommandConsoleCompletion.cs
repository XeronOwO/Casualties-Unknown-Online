using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Application.Kernel;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Session.Content;
using CasualtiesUnknownOnline.Runtime.Session.HostRules;

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
	IHostRules hostRules)
{
	private static readonly CommandSuggestion[] JsonSuggestions =
	[
		new("{}", "Empty JSON object"),
		new("{\"key\": \"value\"}", "JSON object template"),
	];

	private readonly ConsoleCommandRegistry _commands = commands;
	private readonly ISessionControl _session = session;
	private readonly IHostBanService _bans = bans;
	private readonly IResourceLocationCatalog _resourceLocations = resourceLocations;
	private readonly ConsoleCommandAuthority _authority = authority;
	private readonly CheatVerbGate _cheatVerbs = cheatVerbs;
	private readonly ItemStateCompletion _itemState = new(kernelItems, itemStateLanguage, targets);
	private readonly HostRuleCompletion _hostRules = new(hostRules);

	/// <summary>Every completion candidate for the token under the caret, filtered by this caller's authority.</summary>
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

			return [.. _commands.All
				.Where(_authority.MayRun)
				.Where(c => c.Name.StartsWith(partial, StringComparison.OrdinalIgnoreCase))
				.Select(c => new CommandSuggestion(c.Name, c.Description))
				.OrderBy(c => c.Text, StringComparer.OrdinalIgnoreCase)];
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

		// …and so does an argument whose legal words come from the parser the verb reads (a summon position, a give slot).
		if (ArgumentWordCompletion.TrySuggest(command.Name, tokens, current, out var wordSuggestions))
		{
			return wordSuggestions;
		}

		// The write half's SNBT state is the same cheat verb's argument: a caller the gate refuses gets no
		// state vocabulary either, while /data get's own completion is unaffected.
		var dataWriteLocked = isData
			&& tokens.Count > 1
			&& ItemDataVerbParser.IsWriteVerb(tokens[1].Unquoted)
			&& !mayRunCheatVerbs;
		if (!dataWriteLocked && _itemState.TrySuggest(command.Name, tokens, current, out var stateSuggestions))
		{
			return stateSuggestions;
		}

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

	/// <summary>One argument kind's candidates, the narrow surface the UI can also ask directly.</summary>
	internal IReadOnlyList<CommandSuggestion> Suggest(CommandArgumentKind kind, string prefix) => SuggestFor(kind, prefix);

	/// <summary>
	/// The usage hint for a typed command line, or null when there is nothing useful to show. A hint is not
	/// a completion: the input session exposes it for whatever surface renders hints, and it is not what the
	/// suggestion list hides. A hidden command typed in full still reaches the handler, so this line names
	/// what that handler accepts.
	/// </summary>
	internal string? GetHint(string input)
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

	private IReadOnlyList<CommandSuggestion> SuggestFor(CommandArgumentKind kind, string prefix) => kind switch
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
	/// Resource/id completion from the content-id catalog: the user may type a
	/// canonical id prefix, the bare content id, or the localized display name,
	/// but every suggestion is the canonical <c>namespace:path</c> id (the
	/// catalog owns matching/ranking; this method only projects it for the UI).
	/// </summary>
	private IReadOnlyList<CommandSuggestion> SuggestResourceLocations(string prefix) =>
		[.. _resourceLocations.Suggest(prefix)
			.Select(entry => new CommandSuggestion(entry.Id.ToString(), $"{entry.Kind} · {entry.DisplayName}"))];
}
