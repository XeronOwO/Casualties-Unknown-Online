using System;
using System.Collections.Generic;
using System.Linq;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The console's completion for <c>/gamemode</c>'s mode argument: the verb accepts one of two words, and an argument whose legal
/// values are a closed set has to offer them — otherwise the player types it blind, which is the reported "我输命令没有看到补全提示呀"
/// one verb later than the suggestion list itself.
///
/// <para>
/// The candidates are the two words the handler accepts (<c>survivor</c> and <c>spectator</c>, the same pair its usage line and its
/// refusal name), so a suggestion can never be a word the verb refuses. It answers only while the caller may run the verb:
/// <see cref="CommandConsoleCompletion"/> asks the authority before any source, so a member without the cheat grant never sees them.
/// </para>
/// </summary>
internal static class GamemodeCompletion
{
	/// <summary>What <c>/gamemode</c> accepts, in the order the usage line names them.</summary>
	private static readonly string[] Modes = ["survivor", "spectator"];

	/// <summary>Suggestions for the token under the caret, or false when the caret is not on the mode argument.</summary>
	internal static bool TrySuggest(
		string command,
		IReadOnlyList<CommandLineTokenizer.Token> tokens,
		CommandLineTokenizer.Token current,
		out IReadOnlyList<CommandSuggestion> suggestions)
	{
		suggestions = [];
		if (!string.Equals(command, "gamemode", StringComparison.OrdinalIgnoreCase) || !IsModePosition(tokens, current))
		{
			return false;
		}

		var prefix = current.Length == 0 ? "" : current.Unquoted;
		suggestions = [.. Modes
			.Where(mode => mode.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
			.Select(mode => new CommandSuggestion(mode, "Accepted value for /gamemode"))];
		return suggestions.Count > 0;
	}

	/// <summary>The mode argument: after the command (a fresh token) or the word being typed.</summary>
	private static bool IsModePosition(IReadOnlyList<CommandLineTokenizer.Token> tokens, CommandLineTokenizer.Token current) =>
		tokens.Count switch
		{
			1 => current.Length == 0,
			2 => tokens[1].Start == current.Start,
			_ => false,
		};
}
