using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

public class CommandSelectorResolverTests
{
	private static readonly CommandSelectorResolver.Target Local = new(1, true, new NetVector2(0f, 0f));

	private static readonly CommandSelectorResolver.Target Close = new(10, false, new NetVector2(2f, 0f));

	private static readonly CommandSelectorResolver.Target Far = new(11, false, new NetVector2(20f, 0f));

	[Fact]
	public void All_ExpandsToRemotePlayersOnly()
	{
		var result = CommandSelectorResolver.Resolve("@a", [Local, Close, Far]);

		Assert.Equal([10UL, 11UL], result);
	}

	[Fact]
	public void Entities_ExpandsToRemotePlayersOnly()
	{
		var result = CommandSelectorResolver.Resolve("@e", [Local, Close, Far]);

		Assert.Equal([10UL, 11UL], result);
	}

	[Fact]
	public void Self_ExpandsToLocalPlayerOnly()
	{
		var result = CommandSelectorResolver.Resolve("@s", [Local, Close, Far]);

		Assert.Equal([1UL], result);
	}

	[Fact]
	public void Nearest_ReturnsClosestRemotePlayer()
	{
		var result = CommandSelectorResolver.Resolve("@p", [Local, Close, Far]);

		Assert.Equal([10UL], result);
	}

	[Fact]
	public void Random_ReturnsOneRemotePlayer()
	{
		var result = CommandSelectorResolver.Resolve("@r", [Local, Close, Far]);

		var single = Assert.Single(result);
		Assert.Contains(single, [10UL, 11UL]);
	}

	[Fact]
	public void UnknownSelector_ReturnsEmpty()
	{
		var result = CommandSelectorResolver.Resolve("@z", [Local, Close, Far]);

		Assert.Empty(result);
	}

	[Fact]
	public void InvalidInputs_ReturnEmpty()
	{
		Assert.Empty(CommandSelectorResolver.Resolve(null, [Local]));
		Assert.Empty(CommandSelectorResolver.Resolve("", [Local]));
		Assert.Empty(CommandSelectorResolver.Resolve("player", [Local]));
	}

	[Fact]
	public void SelectorNames_AreCaseInsensitive()
	{
		var result = CommandSelectorResolver.Resolve("@A", [Local, Close, Far]);

		Assert.Equal([10UL, 11UL], result);
	}

	[Fact]
	public void NoRemotePlayers_ReturnsEmptyForRemoteSelectors()
	{
		Assert.Empty(CommandSelectorResolver.Resolve("@a", [Local]));
		Assert.Empty(CommandSelectorResolver.Resolve("@p", [Local]));
		Assert.Empty(CommandSelectorResolver.Resolve("@r", [Local]));
		Assert.Empty(CommandSelectorResolver.Resolve("@e", [Local]));
	}

	[Fact]
	public void TargetListPreservesInputOrderForAllSelectors()
	{
		var reversed = new CommandSelectorResolver.Target[] { Far, Close, Local };
		var result = CommandSelectorResolver.Resolve("@a", reversed);

		Assert.Equal([11UL, 10UL], result);
	}
}
