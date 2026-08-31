using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// One runtime command-registry entry: the presentation metadata plus the
/// executable handler. Built-in entries are produced by attribute discovery;
/// mod entries are produced by the Abstractions-backed console command adapter.
/// </summary>
internal sealed record CommandDefinition(
	string Name,
	string Description,
	CommandPermission Permission,
	string Usage,
	IReadOnlyList<CommandArgumentKind> ArgumentKinds,
	Func<IReadOnlyList<string>, string?> Handler)
{
	public CommandSpec ToSpec() => new(Name, Description, Usage, Permission, ArgumentKinds);
}
