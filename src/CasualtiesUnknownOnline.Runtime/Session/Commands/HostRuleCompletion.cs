using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Session.HostRules;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The console's command-specific completion for <c>/gamerule</c>: the second argument completes the
/// census's rule names (each with the value in force, so the list doubles as the query the player wanted),
/// and the third completes that rule's accepted values.
///
/// <para>
/// Every candidate comes from the same sources the handler reads — <see cref="HostRuleCensus"/> for the
/// names and <see cref="HostRuleValueText.Choices"/> for the values — so a suggestion can never be a value
/// the verb refuses. A numeric rule has no candidate list (Minecraft does not complete numbers) and its
/// refusal states the range instead, which is why the third argument is simply empty for it.
/// </para>
///
/// <para>
/// It answers only while the caller may run the verb: <see cref="CommandConsoleCompletion"/> asks the
/// authority before this source, so a member without the grant never sees a rule name.
/// </para>
/// </summary>
internal sealed class HostRuleCompletion(IHostRules hostRules)
{
	private readonly IHostRules _hostRules = hostRules;

	/// <summary>Suggestions for the token under the caret, or false when the caret is not on a gamerule argument this source owns.</summary>
	internal bool TrySuggest(
		string command,
		IReadOnlyList<CommandLineTokenizer.Token> tokens,
		CommandLineTokenizer.Token current,
		out IReadOnlyList<CommandSuggestion> suggestions)
	{
		suggestions = [];
		if (!string.Equals(command, "gamerule", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		var prefix = current.Length == 0 ? "" : current.Unquoted;
		if (IsRuleNamePosition(tokens, current))
		{
			var rules = HostRuleSet.From(_hostRules);
			suggestions = [.. HostRuleCensus.Rows
				.Where(row => row.Name.StartsWith(prefix, StringComparison.Ordinal))
				.Select(row => new CommandSuggestion(row.Name, $"Currently {HostRuleValueText.Describe(rules, row)}"))];
			return suggestions.Count > 0;
		}

		if (!IsValuePosition(tokens, current) || tokens.Count < 2)
		{
			return false;
		}

		// The handler's name lookup is ordinal and case-sensitive, so a misspelled name gets no values:
		// offering the nearest rule's values would suggest a line the verb refuses.
		var rule = HostRuleCensus.Find(tokens[1].Unquoted);
		if (rule is null)
		{
			return false;
		}

		suggestions = [.. HostRuleValueText.Choices(rule)
			.Where(choice => choice.StartsWith(prefix, StringComparison.Ordinal))
			.Select(choice => new CommandSuggestion(choice, $"Accepted value for {rule.Name}"))];
		return suggestions.Count > 0;
	}

	/// <summary>The second argument: after the command (a fresh token) or the rule name being typed.</summary>
	private static bool IsRuleNamePosition(IReadOnlyList<CommandLineTokenizer.Token> tokens, CommandLineTokenizer.Token current) =>
		tokens.Count switch
		{
			1 => current.Length == 0,
			2 => tokens[1].Start == current.Start,
			_ => false,
		};

	/// <summary>The third argument: after a complete rule name plus a space, or the value being typed.</summary>
	private static bool IsValuePosition(IReadOnlyList<CommandLineTokenizer.Token> tokens, CommandLineTokenizer.Token current) =>
		tokens.Count switch
		{
			2 => current.Length == 0,
			3 => tokens[2].Start == current.Start,
			_ => false,
		};
}
