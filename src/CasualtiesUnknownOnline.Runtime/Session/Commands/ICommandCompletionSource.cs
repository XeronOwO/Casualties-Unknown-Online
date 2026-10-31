using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The completion/hint side of the command console. It is deliberately separate
/// from <see cref="ICommandControl"/> so the input session can depend on a pure
/// suggestion source while the execution surface stays narrow.
/// </summary>
public interface ICommandCompletionSource
{
	/// <summary>
	/// Read-only command metadata, ordered by registration and FILTERED to what this caller may run: a
	/// command the console would hide from the suggestion list is not named here either. The enforcement is
	/// still the handler's; this projection exists so a presentation surface cannot leak the word.
	/// </summary>
	IReadOnlyList<CommandSpec> Commands { get; }

	/// <summary>
	/// Returns completion candidates for the current command-line token. The
	/// caller (input session) owns inserting/quoting the chosen candidate.
	/// </summary>
	IReadOnlyList<CommandSuggestion> Suggest(string input);

	/// <summary>
	/// The hint about the line being typed (a catalogue key and its arguments), or null when the line has nothing to explain. The caller renders it in the
	/// reader's language — this surface never returns a finished sentence.
	/// </summary>
	CommandHint? GetHint(string input);
}
