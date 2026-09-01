using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

public class ConsoleCommandTreeTests
{
	[Fact]
	public void FromArgumentKinds_BuildsOrderedArgumentNodes()
	{
		var tree = ConsoleCommandTree.FromArgumentKinds([CommandArgumentKind.Selector, CommandArgumentKind.ResourceLocation]);

		Assert.Equal(2, tree.Nodes.Count);
		Assert.Equal(CommandArgumentKind.Selector, tree.GetArgumentKind(0));
		Assert.Equal(CommandArgumentKind.ResourceLocation, tree.GetArgumentKind(1));
	}

	[Fact]
	public void GetArgumentKind_OutOfRangeReturnsNull()
	{
		var tree = ConsoleCommandTree.FromArgumentKinds([CommandArgumentKind.Text]);

		Assert.Null(tree.GetArgumentKind(1));
		Assert.Null(tree.GetArgumentKind(-1));
	}

	[Fact]
	public void LiteralNode_KeepsLiteralAndNoArgumentKind()
	{
		var node = CommandNode.CreateLiteral("set", "Set a value");

		Assert.Equal(CommandNodeKind.Literal, node.Kind);
		Assert.Equal("set", node.Literal);
		Assert.Null(node.ArgumentKind);
	}
}
