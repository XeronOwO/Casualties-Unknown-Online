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

	[Fact]
	public void BracketedTypeFilter_AcceptsPlayerAndRejectsOtherTypes()
	{
		var withNames = new[] { Local, Close with { DisplayName = "Alice" }, Far with { DisplayName = "Bob" } };

		Assert.Equal([10UL, 11UL], CommandSelectorResolver.Resolve("@a[type=player]", withNames));
		Assert.Empty(CommandSelectorResolver.Resolve("@a[type=zombie]", withNames));
	}

	[Fact]
	public void BracketedNameFilter_MatchesCaseInsensitively()
	{
		var withNames = new[] { Local, Close with { DisplayName = "Alice" }, Far with { DisplayName = "Bob" } };

		Assert.Equal([10UL], CommandSelectorResolver.Resolve("@a[name=alice]", withNames));
		Assert.Empty(CommandSelectorResolver.Resolve("@a[name=charlie]", withNames));
	}

	[Fact]
	public void BracketedDistanceLimitSort_FilterAndOrder()
	{
		var withNames = new[] { Local, Close, Far };

		Assert.Equal([10UL], CommandSelectorResolver.Resolve("@a[distance=2..10]", withNames));
		Assert.Equal([11UL], CommandSelectorResolver.Resolve("@a[distance=20..99,sort=nearest]", withNames));
		Assert.Equal([10UL], CommandSelectorResolver.Resolve("@e[distance=1..99,limit=1,sort=nearest]", withNames));
	}

	[Fact]
	public void IncompleteOrUnknownBracketFilters_ReturnEmpty()
	{
		var withNames = new[] { Local, Close, Far };

		Assert.Empty(CommandSelectorResolver.Resolve("@a[type=player", withNames));
		Assert.Empty(CommandSelectorResolver.Resolve("@a[unknown=1]", withNames));
	}
}
