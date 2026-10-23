using System.Collections.Generic;
using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// Immutable public command metadata for the UI/input layer. The execution
/// handler stays private inside <see cref="CommandConsoleService"/>; this spec
/// is only the presentation-facing projection of the registry. <see cref="Gate"/>
/// says whether the verb's authority is the declared permission alone or the
/// cheat family's gate, so a presentation surface can classify it without
/// duplicating the console's table.
/// </summary>
public sealed record CommandSpec(
	string Name,
	string Description,
	string Usage,
	CommandPermission Permission,
	ConsoleCommandGate Gate,
	IReadOnlyList<CommandArgumentKind> ArgumentKinds);
