using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// Answers what the token under the cursor may be, given the arguments already accepted in this line. This is the whole contract a mod's own completion
/// implements: it reads the context and returns candidates, or returns none.
/// </summary>
/// <remarks>
/// The framework owns the list's behaviour — ordering, the cap, the keys (Tab/↑/↓), quoting on insertion and the authority filter — so a completer can only
/// PROPOSE. It runs inside a frame, so it must be cheap; a completer that throws or hangs is isolated by the console and reported as "this argument offered
/// nothing" rather than taking the overlay down.
/// </remarks>
public interface IArgumentCompleter
{
	/// <summary>The candidates for the current token, in the completer's own preferred order.</summary>
	IReadOnlyList<CommandCompletion> Complete(ICompletionContext context);
}
