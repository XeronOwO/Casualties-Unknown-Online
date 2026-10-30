namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// The shipped parameter a mod builds: a name, an optional completer, an optional hint key, and the framework source it falls back to.
/// </summary>
/// <remarks>
/// The ten <see cref="CommandArgumentKind"/> sources stay — a definition composes with them instead of replacing them, so a mod that only wants "a member name"
/// or "an item id" declares the kind and gets the framework's own suggestions (and its ranking), while a mod with its own vocabulary supplies an
/// <see cref="IArgumentCompleter"/> on top.
/// </remarks>
public sealed class ConsoleCommandParameter(
	string name,
	IArgumentCompleter? completer = null,
	string? hintKey = null,
	CommandArgumentKind kind = CommandArgumentKind.Text) : ICommandParameter
{
	public string Name { get; } = name;

	public IArgumentCompleter? Completer { get; } = completer;

	public string? HintKey { get; } = hintKey;

	/// <summary>The framework source this parameter falls back to when it offers no completer of its own.</summary>
	public CommandArgumentKind Kind { get; } = kind;
}
