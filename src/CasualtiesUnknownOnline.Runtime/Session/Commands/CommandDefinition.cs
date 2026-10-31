using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// One runtime command-registry entry: the presentation metadata plus the
/// executable handler. Built-in entries are produced by attribute discovery;
/// mod entries are produced by the Abstractions-backed console command adapter.
/// The handler answers with a <see cref="ConsoleAnswer"/>, so the console knows
/// whether the line is a success or a refusal without reading the text.
/// </summary>
internal sealed record CommandDefinition(
	string Name,
	string Description,
	CommandPermission Permission,
	ConsoleCommandGate Gate,
	string Usage,
	IReadOnlyList<CommandArgumentKind> ArgumentKinds,
	Func<IReadOnlyList<string>, ConsoleAnswer> Answer,
	IReadOnlyList<string>? Aliases = null,
	ICommandDefinition? Contract = null,
	string? OwnerModId = null,
	Func<ulong, IReadOnlyList<string>, ConsoleAnswer>? RelayAnswer = null)
{
	public IReadOnlyList<string> Aliases { get; } = Aliases ?? [];

	public CommandSpec ToSpec() => new(Name, Description, Usage, Permission, Gate, ArgumentKinds, Aliases);
}
