using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// One candidate a completer proposes: the text that replaces the token, and — optionally — the catalogue key and its arguments for the explanation drawn
/// beside it. The framework decides whether the text is inserted verbatim or quoted; a candidate never carries a finished sentence, because the client that
/// draws it renders it in its own language.
/// </summary>
public sealed class CommandCompletion(string text, string? hintKey = null, IReadOnlyList<object?>? hintArgs = null, bool verbatim = false)
{
	/// <summary>The token text the console inserts.</summary>
	public string Text { get; } = text;

	/// <summary>The catalogue key of the explanation beside this candidate (<c>console.suggest.*</c> or a mod's own key); <c>null</c> for a bare candidate.</summary>
	public string? HintKey { get; } = hintKey;

	/// <summary>The values <see cref="HintKey"/>'s sentence places, in order.</summary>
	public IReadOnlyList<object?> HintArgs { get; } = hintArgs ?? [];

	/// <summary>
	/// Insert <see cref="Text"/> as the whole token without the console's quoting — for a candidate that is already one command-line token (an item state with
	/// braces and quotes of its own).
	/// </summary>
	public bool Verbatim { get; } = verbatim;
}
