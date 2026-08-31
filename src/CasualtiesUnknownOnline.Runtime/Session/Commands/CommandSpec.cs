using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// Immutable public command metadata for the UI/input layer. The execution
/// handler stays private inside <see cref="CommandConsoleService"/>; this spec
/// is only the presentation-facing projection of the registry.
/// </summary>
public sealed record CommandSpec(
	string Name,
	string Description,
	string Usage,
	CommandPermission Permission,
	IReadOnlyList<CommandArgumentKind> ArgumentKinds);
