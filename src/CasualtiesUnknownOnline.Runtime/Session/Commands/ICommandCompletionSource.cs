using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The completion/hint side of the command console. It is deliberately separate
/// from <see cref="ICommandControl"/> so the input session can depend on a pure
/// suggestion source while the execution surface stays narrow.
/// </summary>
public interface ICommandCompletionSource
{
	/// <summary>Read-only command metadata, ordered by registration.</summary>
	IReadOnlyList<CommandSpec> Commands { get; }

	/// <summary>
	/// Returns completion candidates for the current command-line token. The
	/// caller (input session) owns inserting/quoting the chosen candidate.
	/// </summary>
	IReadOnlyList<CommandSuggestion> Suggest(string input);

	/// <summary>
	/// Returns a short usage/hint line for the current command line, or null
	/// when there is nothing useful to show.
	/// </summary>
	string? GetHint(string input);
}
