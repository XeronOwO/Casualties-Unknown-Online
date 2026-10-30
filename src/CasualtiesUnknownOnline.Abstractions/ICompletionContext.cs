using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// What a completer is told about the line being typed: the command (by its registered name), which parameter is under the cursor, the arguments already
/// accepted before it, the partial token, and the caller's own session snapshot.
/// </summary>
/// <remarks>
/// The arguments are the tokens BEFORE the cursor's parameter, already unquoted — that is what makes argument N able to depend on N-1 (the user's
/// `/gamerule rule value` and a mod's own key/value pair). The session snapshot is a READ of the current frame: it is not a promise about the invocation, which
/// re-reads the session inside the handler.
/// </remarks>
public interface ICompletionContext
{
	/// <summary>The registered command name (not the alias the player may have typed).</summary>
	string CommandName { get; }

	/// <summary>Zero-based index of the parameter being completed.</summary>
	int ParameterIndex { get; }

	/// <summary>The arguments already accepted before this parameter, unquoted and in order.</summary>
	IReadOnlyList<string> Arguments { get; }

	/// <summary>The part of the current token the player has typed; empty at a fresh word.</summary>
	string Partial { get; }

	/// <summary>The caller's session snapshot at completion time.</summary>
	ISessionInfo Session { get; }
}
