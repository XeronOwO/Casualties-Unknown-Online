using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// Argument-type-level suggestion surface. The full command completion source
/// (<see cref="ICommandCompletionSource"/>) combines command parsing with these
/// per-argument providers; this narrow seam lets callers request suggestions for
/// any declared argument kind without reimplementing command parsing.
/// </summary>
public interface ICommandArgumentSuggestions
{
	/// <summary>Returns candidates for one argument kind, filtered by the current prefix.</summary>
	IReadOnlyList<CommandSuggestion> Suggest(CommandArgumentKind kind, string prefix);
}
