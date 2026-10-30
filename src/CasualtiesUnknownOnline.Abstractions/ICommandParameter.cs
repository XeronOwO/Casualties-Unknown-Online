namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// One parameter of a <see cref="ICommandDefinition"/>: the name a hint/usage row shows, the completer that answers what the next token may be, and —
/// when the author wants it — the catalogue key of the grey explanation beside the completion row.
/// </summary>
/// <remarks>
/// The parameter does NOT parse: the framework owns tokenising, quoting and the order of the arguments, and hands the handler the raw tokens. A parameter
/// that needs a typed value can be parsed by the handler itself, which keeps one place (the framework) responsible for what a line means syntactically and
/// one place (the command) responsible for what it means for the game.
/// </remarks>
public interface ICommandParameter
{
	/// <summary>The parameter's name, used by usage text and by a refusal that names the argument it expected.</summary>
	string Name { get; }

	/// <summary>What the next token may be, with the arguments already accepted in hand; <c>null</c> means "offer nothing" and never blocks typing.</summary>
	IArgumentCompleter? Completer { get; }

	/// <summary>The catalogue key of the explanation drawn beside this parameter's candidates, or <c>null</c> for none.</summary>
	string? HintKey { get; }

	/// <summary>
	/// The framework source this parameter falls back to when <see cref="Completer"/> is <c>null</c> — the ten shipped kinds stay, so a definition composes with
	/// them instead of replacing them.
	/// </summary>
	CommandArgumentKind Kind { get; }
}
