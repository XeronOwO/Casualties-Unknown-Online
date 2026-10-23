using System;
using System.Collections.Generic;
using System.Linq;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The console's command-specific completion for <c>/data</c>'s verb word: <c>get</c> for every caller,
/// and the write half (<c>merge</c>, <c>modify</c>, <c>remove</c>) only for a caller the cheat gate admits.
///
/// <para>
/// The command WORD itself stays offered to everyone: it is one command with a read half that everyone may
/// run, so hiding the word would hide the read. The per-verb decision is taken from
/// <see cref="ItemDataVerbParser.IsWriteVerb"/> — the same classification the handler dispatches on — so
/// the completion list and the gate cannot drift.
/// </para>
/// </summary>
internal static class ItemDataVerbCompletion
{
	/// <summary>Suggestions for the verb word under the caret, or false when the caret is not on that word.</summary>
	internal static bool TrySuggest(
		string command,
		IReadOnlyList<CommandLineTokenizer.Token> tokens,
		CommandLineTokenizer.Token current,
		bool mayRunCheatVerbs,
		out IReadOnlyList<CommandSuggestion> suggestions)
	{
		suggestions = [];
		if (!string.Equals(command, "data", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		if (!IsVerbPosition(tokens, current))
		{
			return false;
		}

		var prefix = current.Length == 0 ? "" : current.Unquoted;
		var words = new List<CommandSuggestion>
		{
			new("get", "Read one item's recorded state"),
		};
		if (mayRunCheatVerbs)
		{
			words.Add(new("merge", "Merge properties into an item's recorded state"));
			words.Add(new("modify", "Modify one path of an item's recorded state"));
			words.Add(new("remove", "Remove one path from an item's recorded state"));
		}

		suggestions = [.. words.Where(word => word.Text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))];
		return suggestions.Count > 0;
	}

	/// <summary>The verb is the second token: a fresh word after the command, or that word being typed.</summary>
	private static bool IsVerbPosition(IReadOnlyList<CommandLineTokenizer.Token> tokens, CommandLineTokenizer.Token current) =>
		tokens.Count switch
		{
			1 => current.Length == 0,
			2 => tokens[1].Start == current.Start,
			_ => false,
		};
}
