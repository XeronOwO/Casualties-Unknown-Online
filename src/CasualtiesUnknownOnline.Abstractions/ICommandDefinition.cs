using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// One console command, as a MOD declares it: the shape (name, aliases, parameters) and the behaviour, in one object the framework can register while the
/// game runs. Built-in commands produce the same shape from their attribute, so the registry, the completer and the dispatcher speak one vocabulary instead of
/// two dialects (user direction 2026-10-27, shape decided 2026-10-30).
/// </summary>
/// <remarks>
/// A definition is asked in this order and each answer is a FACT, not prose: the authority decides whether the caller may run it
/// (<see cref="Permission"/>, and the host's rules for a <see cref="CommandScope.HostRelayed"/> one), the completer answers what the next token may be, and
/// <see cref="Invoke"/> runs the command. Localisation rides the same object: <see cref="DescriptionKey"/> and each parameter's hint key name catalogue entries
/// the receiving client renders in its own language, while <see cref="Description"/> and <see cref="Usage"/> are the log/fallback sentences.
/// </remarks>
public interface ICommandDefinition
{
	/// <summary>The word the player types after the slash. Lowercase, no spaces.</summary>
	string Name { get; }

	/// <summary>Other words that reach this command (a mod may offer a short form); completion offers both.</summary>
	IReadOnlyList<string> Aliases { get; }

	/// <summary>The one-line description in the author's language: the log's sentence and the fallback when the catalogue has no key.</summary>
	string Description { get; }

	/// <summary>The catalogue key for <see cref="Description"/>, when the mod ships one in both languages; <c>null</c> otherwise.</summary>
	string? DescriptionKey { get; }

	/// <summary>The usage line, with the identifiers the player must type (<c>&lt;steamId&gt;</c>, a command name) left as they are.</summary>
	string Usage { get; }

	/// <summary>Who may run it, before the host's own rules are asked.</summary>
	CommandPermission Permission { get; }

	/// <summary>Whether the command runs in this process or on the authority (see <see cref="CommandScope"/>).</summary>
	CommandScope Scope { get; }

	/// <summary>The parameters in order; empty for a command that takes none.</summary>
	IReadOnlyList<ICommandParameter> Parameters { get; }

	/// <summary>
	/// Runs the command. The invocation carries the typed arguments, the alias the player used, and the caller's own identity and session snapshot.
	/// Throwing is isolated by the console and reported as a refusal, but a definition should answer instead of throwing.
	/// </summary>
	ConsoleAnswer Invoke(ICommandInvocation invocation);
}
