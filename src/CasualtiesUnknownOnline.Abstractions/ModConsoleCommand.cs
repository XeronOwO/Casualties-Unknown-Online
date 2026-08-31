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
	Func<IModConsoleCommandContext, string?> handler)
{
	public string Name { get; } = name;

	public string Description { get; } = description;

	public string Usage { get; } = usage;

	public CommandPermission Permission { get; } = permission;

	public IReadOnlyList<CommandArgumentKind> ArgumentKinds { get; } = [.. argumentKinds];

	/// <summary>The command body. Return the output text (null = no output); throwing is isolated by the console.</summary>
	public Func<IModConsoleCommandContext, string?> Handler { get; } = handler;
}
