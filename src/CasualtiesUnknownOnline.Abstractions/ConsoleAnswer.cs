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
/// </summary>
public readonly struct ConsoleAnswer
{
	private ConsoleAnswer(string? text, ConsoleAnswerKind kind, string? key = null, string[]? args = null)
	{
		Text = text;
		Kind = kind;
		Key = key;
		Args = args;
	}

	/// <summary>The line the console prints, or null when there is nothing to print.</summary>
	public string? Text { get; }

	/// <summary>Whether that line is a success or a refusal.</summary>
	public ConsoleAnswerKind Kind { get; }

	/// <summary>The catalogue key for <see cref="Text"/> when the sentence comes from a closed set, otherwise null.</summary>
	public string? Key { get; }

	/// <summary>Arguments for <see cref="Key"/>'s sentence, in the order the catalogue places them.</summary>
	public IReadOnlyList<string>? Args { get; }

	/// <summary>True when the console must print this answer at all.</summary>
	public bool HasText => !string.IsNullOrWhiteSpace(Text);

	/// <summary>The command finished and has nothing to say.</summary>
	public static ConsoleAnswer Silent => new(null, ConsoleAnswerKind.Success);

	/// <summary>The command finished, with a line for the console.</summary>
	public static ConsoleAnswer Ok(string? text) => new(text, ConsoleAnswerKind.Success);

	/// <summary>A success whose sentence the catalogue owns, so the console prints it in the client's language.</summary>
	public static ConsoleAnswer Ok(string text, string key, params string[] args) => new(text, ConsoleAnswerKind.Success, key, args);

	/// <summary>The command refused, with the reason the player reads.</summary>
	public static ConsoleAnswer Refused(string text) => new(text, ConsoleAnswerKind.Refusal);

	/// <summary>A refusal whose sentence the catalogue owns, so the console prints it in the client's language.</summary>
	public static ConsoleAnswer Refused(string text, string key, params string[] args) => new(text, ConsoleAnswerKind.Refusal, key, args);
}
