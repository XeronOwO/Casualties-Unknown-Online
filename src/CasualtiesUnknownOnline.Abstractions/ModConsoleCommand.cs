using System;
using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// A local console command a mod registers via <see cref="IModConsoleCommands"/>
/// (typically in <see cref="ICuoMod.Bind"/>). Unlike <see cref="ModCommand"/>,
/// this command is strictly local: it appears only in the local in-game
/// command console and never travels over the network. The handler runs on
/// whichever side invokes the console line.
/// </summary>
public sealed class ModConsoleCommand(
	string name,
	string description,
	string usage,
	CommandPermission permission,
	IReadOnlyList<CommandArgumentKind> argumentKinds,
	Func<IModConsoleCommandContext, ConsoleAnswer> handler)
{
	public string Name { get; } = name;

	public string Description { get; } = description;

	public string Usage { get; } = usage;

	public CommandPermission Permission { get; } = permission;

	public IReadOnlyList<CommandArgumentKind> ArgumentKinds { get; } = argumentKinds is null ? [] : [.. argumentKinds];

	/// <summary>
	/// The command body: answers with <see cref="ConsoleAnswer"/>, so the console knows whether the line is a
	/// completed action or a refusal (an old handler that returned text is now a <c>ConsoleAnswer.Ok</c>). Throwing
	/// is isolated by the console.
	/// </summary>
	public Func<IModConsoleCommandContext, ConsoleAnswer> Handler { get; } = handler;
}
