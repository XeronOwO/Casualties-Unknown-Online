using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// An immutable command-tree node. The current built-in commands are flat
/// argument chains, but the tree shape is the intended model for future literal
/// subcommands and resource-location branches.
/// </summary>
internal sealed record CommandNode(
	CommandNodeKind Kind,
	CommandArgumentKind? ArgumentKind = null,
	string? Literal = null,
	string? Description = null)
{
	public static CommandNode Argument(CommandArgumentKind kind, string? description = null) =>
		new(CommandNodeKind.Argument, kind, null, description);

	public static CommandNode CreateLiteral(string literal, string? description = null) =>
		new(CommandNodeKind.Literal, null, literal, description);
}
