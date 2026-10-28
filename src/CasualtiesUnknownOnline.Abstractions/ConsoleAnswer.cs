namespace CasualtiesUnknownOnline.Abstractions;

using System.Collections.Generic;

/// <summary>
/// One console command's answer: the text to print (null or empty = nothing to print) plus whether it is a refusal.
/// A handler returns this instead of a bare string, so a refusal can never be printed as a success by forgetting to
/// mark it. A plain struct rather than a record: this assembly targets .NET Framework 4.8 and carries no
/// <c>IsExternalInit</c> polyfill.
///
/// <para>
/// An answer may also carry the catalogue KEY and arguments for its sentence. The text stays on the answer (the log, the
/// fallback language and any client without the entry read it), and the console renders the key in the client's own language
/// when it has one. A handler that knows its reason from a closed set (the cheat gate's grounds, a command's own refusal kinds)
/// passes a key; a sentence assembled from free data does not.
/// </para>
///
/// <para>
/// A multi-LINE answer (one line per target, as <c>/heal @a</c> is) carries <see cref="Parts"/> instead: each part is a key plus
/// its arguments, and the console renders each of them through the local catalogue. Without that shape the host's own answer —
/// which never travels over the wire — had only its assembled English text, which is how a Chinese host read English after
/// <c>/heal @a</c> while the same answer reached a member correctly (measured 2026-10-28).
/// </para>
/// </summary>
public readonly struct ConsoleAnswer
{
	private ConsoleAnswer(string? text, ConsoleAnswerKind kind, string? key = null, string[]? args = null, IReadOnlyList<ConsoleAnswerPart>? parts = null)
	{
		Text = text;
		Kind = kind;
		Key = key;
		Args = args;
		Parts = parts;
	}

	/// <summary>The line the console prints, or null when there is nothing to print.</summary>
	public string? Text { get; }

	/// <summary>Whether that line is a success or a refusal.</summary>
	public ConsoleAnswerKind Kind { get; }

	/// <summary>The catalogue key for <see cref="Text"/> when the sentence comes from a closed set, otherwise null.</summary>
	public string? Key { get; }

	/// <summary>Arguments for <see cref="Key"/>'s sentence, in the order the catalogue places them.</summary>
	public IReadOnlyList<string>? Args { get; }

	/// <summary>
	/// A multi-LINE answer: one <see cref="ConsoleAnswerPart"/> per line, each a catalogue key plus its arguments, rendered by the reader's own
	/// catalogue. Null or empty for the ordinary one-sentence answer (which uses <see cref="Key"/>).
	/// </summary>
	public IReadOnlyList<ConsoleAnswerPart>? Parts { get; }

	/// <summary>True when this answer is one line PER PART rather than one sentence.</summary>
	public bool HasParts => Parts is { Count: > 0 };

	/// <summary>True when the console must print this answer at all.</summary>
	public bool HasText => !string.IsNullOrWhiteSpace(Text);

	/// <summary>The command finished and has nothing to say.</summary>
	public static ConsoleAnswer Silent => new(null, ConsoleAnswerKind.Success);

	/// <summary>The command finished, with a line for the console.</summary>
	public static ConsoleAnswer Ok(string? text) => new(text, ConsoleAnswerKind.Success);

	/// <summary>A success whose sentence the catalogue owns, so the console prints it in the client's language.</summary>
	public static ConsoleAnswer Ok(string text, string key, params string[] args) => new(text, ConsoleAnswerKind.Success, key, args);

	/// <summary>A success that is one line per part, each rendered by the reader's catalogue (<c>/heal @a</c> answers one line per body).</summary>
	public static ConsoleAnswer Ok(string? text, IReadOnlyList<ConsoleAnswerPart> parts) =>
		new(text, ConsoleAnswerKind.Success, parts: parts);

	/// <summary>The command refused, with the reason the player reads.</summary>
	public static ConsoleAnswer Refused(string text) => new(text, ConsoleAnswerKind.Refusal);

	/// <summary>A refusal whose sentence the catalogue owns, so the console prints it in the client's language.</summary>
	public static ConsoleAnswer Refused(string text, string key, params string[] args) => new(text, ConsoleAnswerKind.Refusal, key, args);

	/// <summary>A refusal that is one line per part.</summary>
	public static ConsoleAnswer Refused(string? text, IReadOnlyList<ConsoleAnswerPart> parts) =>
		new(text, ConsoleAnswerKind.Refusal, parts: parts);
}
