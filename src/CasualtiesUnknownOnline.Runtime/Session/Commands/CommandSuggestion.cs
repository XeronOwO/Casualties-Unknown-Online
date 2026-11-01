namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// One completion candidate. The input session uses only <see cref="Text"/> for the insertion; a UI may show <see cref="Hint"/> beside it.
///
/// <para>
/// The hint is a TYPED fact (<see cref="CommandSuggestionHint"/>) plus its arguments, never a finished English sentence: the console is read by the
/// player whose client draws it, so this half must not decide the wording (user's report, 2026-10-25: the completion descriptions stayed English while
/// the client's language was Chinese).
/// </para>
/// </summary>
/// <param name="Text">The token text the session inserts.</param>
/// <param name="Hint">What the hint row means, resolved to text by the renderer's catalogue.</param>
/// <param name="Verbatim">
/// Insert <see cref="Text"/> as the whole token, without the console's own token quoting: an item state is already a command-line token (braced, with
/// its own quoted strings), and quoting it again would rewrite the operator's text and double its escapes.
/// </param>
/// <param name="Args">The hint's parts: a command name, a filter key, a rule name, a value.</param>
public sealed record CommandSuggestion(
	string Text,
	CommandSuggestionHint Hint = CommandSuggestionHint.None,
	bool Verbatim = false,
	params object?[] Args)
{
	/// <summary>
	/// The insertion ENDS this grammatical position (an opening brace, a key and its colon, a whole value), so the next keystroke must recompute the list instead of
	/// cycling the one the position used to have (user correction 2026-10-31: after the brace, Tab must complete the keys inside it, not walk the old ids again).
	/// </summary>
	public bool ResetCycle { get; init; }

	/// <summary>
	/// How the command is called, when this row offers a COMMAND: its own registered usage (`/give &lt;item&gt; [count]`). The console draws it in
	/// the row''s hint, so a player reading a candidate never has to look for a separate usage line (user direction 2026-11-02: the usage line
	/// sat where the list opens and was covered by it). Syntax, not prose: it is the same text in every language, so it is not a catalogue key.
	/// </summary>
	public string? Usage { get; init; }
}
