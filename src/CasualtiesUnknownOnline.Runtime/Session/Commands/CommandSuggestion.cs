namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// One completion candidate with an optional human-readable description. The UI
/// can show the description as a hint/tooltip row; the input session uses only
/// <see cref="Text"/> for the actual insertion.
/// </summary>
/// <param name="Text">The token text the session inserts.</param>
/// <param name="Description">An optional hint row.</param>
/// <param name="Verbatim">
/// Insert <see cref="Text"/> as the whole token, without the console's own token quoting:
/// an item state is already a command-line token (braced, with its own quoted strings), and
/// quoting it again would rewrite the operator's text and double its escapes.
/// </param>
public sealed record CommandSuggestion(string Text, string? Description = null, bool Verbatim = false);
