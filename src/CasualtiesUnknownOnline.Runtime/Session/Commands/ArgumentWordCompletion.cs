using System;
using System.Collections.Generic;
using System.Linq;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The console's completion for the <c>/summon</c> and <c>/give</c> arguments that are not a kind of their own: the position word a
/// summon takes, the <c>slot</c> keyword a give takes, and the argument after them.
///
/// <para>
/// Every candidate is a word the parser accepts — the three position keywords are the parser's own vocabulary and the keyword comes
/// from the give parser's usage — so a suggestion can never be a word the verb refuses. A number is NOT completed: neither grammar
/// bounds it, and inventing a range would offer values the verb may refuse.
/// </para>
///
/// <para>
/// It answers only while the caller may run the verb, because <see cref="CommandConsoleCompletion"/> asks the authority first.
/// </para>
/// </summary>
internal static class ArgumentWordCompletion
{
	/// <summary>The position vocabulary <c>/summon</c> accepts, in the order its own help names them.</summary>
	private static readonly string[] Positions = ["cursor", "player", "random"];

	/// <summary>Suggestions for the token under the caret, or false when the caret is not on a position or a give slot keyword.</summary>
	internal static bool TrySuggest(
		string command,
		IReadOnlyList<CommandLineTokenizer.Token> tokens,
		CommandLineTokenizer.Token current,
		out IReadOnlyList<CommandSuggestion> suggestions)
	{
		suggestions = [];
		var prefix = current.Length == 0 ? "" : current.Unquoted;

		if (string.Equals(command, "summon", StringComparison.OrdinalIgnoreCase))
		{
			if (!IsPositionSlot(tokens, current))
			{
				return false;
			}

			suggestions = [.. Positions
				.Where(position => position.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
				.Select(position => new CommandSuggestion(position, CommandSuggestionHint.SummonPosition))];
			return suggestions.Count > 0;
		}

		if (!string.Equals(command, "give", StringComparison.OrdinalIgnoreCase) || !IsSlotSlot(tokens, current))
		{
			return false;
		}

		suggestions = "slot".StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
			? [new CommandSuggestion("slot", CommandSuggestionHint.BodySlot)]
			: [];
		return suggestions.Count > 0;
	}

	/// <summary>
	/// The position argument: the second argument of the entity form, the third of <c>/summon item</c>. A token that starts with
	/// <c>{</c> is the state, not a position, which the parser states too.
	/// </summary>
	private static bool IsPositionSlot(IReadOnlyList<CommandLineTokenizer.Token> tokens, CommandLineTokenizer.Token current)
	{
		var argument = tokens.Count > 1 && string.Equals(tokens[1].Unquoted, WorldSummonParser.ItemKeyword, StringComparison.OrdinalIgnoreCase)
			? 3
			: 2;
		return AtPosition(tokens, current, argument) && (current.Length == 0 || current.Unquoted[0] != '{');
	}

	/// <summary>
	/// The <c>slot</c> keyword of <c>/give</c>: the third argument (target, item, slot), with the same optional-selector rule the give
	/// parser applies when it reads the tokens.
	/// </summary>
	private static bool IsSlotSlot(IReadOnlyList<CommandLineTokenizer.Token> tokens, CommandLineTokenizer.Token current)
	{
		var argument = tokens.Count > 1 && tokens[1].Unquoted.StartsWith("@", StringComparison.Ordinal) ? 3 : 2;
		return AtPosition(tokens, current, argument);
	}

	/// <summary>
	/// True when the caret is on argument <paramref name="argument"/> (counting the verb as 0): a fresh empty token after it, or the
	/// argument's own token while it is being typed.
	/// </summary>
	private static bool AtPosition(IReadOnlyList<CommandLineTokenizer.Token> tokens, CommandLineTokenizer.Token current, int argument) =>
		tokens.Count == argument ? current.Length == 0
		: tokens.Count == argument + 1 && tokens[argument].Start == current.Start;
}
