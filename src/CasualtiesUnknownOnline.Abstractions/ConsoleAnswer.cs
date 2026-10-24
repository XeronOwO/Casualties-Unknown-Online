namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// One console command's answer: the text to print (null or empty = nothing to print) plus whether it is a refusal.
/// A handler returns this instead of a bare string, so a refusal can never be printed as a success by forgetting to
/// mark it. A plain struct rather than a record: this assembly targets .NET Framework 4.8 and carries no
/// <c>IsExternalInit</c> polyfill.
/// </summary>
public readonly struct ConsoleAnswer
{
	private ConsoleAnswer(string? text, ConsoleAnswerKind kind)
	{
		Text = text;
		Kind = kind;
	}

	/// <summary>The line the console prints, or null when there is nothing to print.</summary>
	public string? Text { get; }

	/// <summary>Whether that line is a success or a refusal.</summary>
	public ConsoleAnswerKind Kind { get; }

	/// <summary>True when the console must print this answer at all.</summary>
	public bool HasText => !string.IsNullOrWhiteSpace(Text);

	/// <summary>The command finished and has nothing to say.</summary>
	public static ConsoleAnswer Silent => new(null, ConsoleAnswerKind.Success);

	/// <summary>The command finished, with a line for the console.</summary>
	public static ConsoleAnswer Ok(string? text) => new(text, ConsoleAnswerKind.Success);

	/// <summary>The command refused, with the reason the player reads.</summary>
	public static ConsoleAnswer Refused(string text) => new(text, ConsoleAnswerKind.Refusal);
}
