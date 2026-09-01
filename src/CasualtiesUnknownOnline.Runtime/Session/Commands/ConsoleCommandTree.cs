using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// The immutable command-tree projection used by console completion. It is
/// currently built from a command's flat argument-kind list, which is the
/// degenerate linear tree; literal subcommands can be layered onto the same
/// node model later without changing the suggestion seam.
/// </summary>
internal sealed class ConsoleCommandTree(IReadOnlyList<CommandNode> nodes)
{
	private readonly IReadOnlyList<CommandNode> _nodes = [.. nodes];

	/// <summary>All argument/literal nodes, in the order they appear after the command name.</summary>
	public IReadOnlyList<CommandNode> Nodes => _nodes;

	/// <summary>Builds a linear tree from a command's declared argument kinds.</summary>
	public static ConsoleCommandTree FromArgumentKinds(IReadOnlyList<CommandArgumentKind> kinds) =>
		new([.. kinds.Select(k => CommandNode.Argument(k))]);

	/// <summary>Returns the node at the given zero-based argument position, or null.</summary>
	public CommandNode? GetNode(int index) => index >= 0 && index < _nodes.Count ? _nodes[index] : null;

	/// <summary>Returns the argument kind at the given zero-based position for argument nodes.</summary>
	public CommandArgumentKind? GetArgumentKind(int index) =>
		GetNode(index) is { Kind: CommandNodeKind.Argument, ArgumentKind: var kind } ? kind : null;
}
