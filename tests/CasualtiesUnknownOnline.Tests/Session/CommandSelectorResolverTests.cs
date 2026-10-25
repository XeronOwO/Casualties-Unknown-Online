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
	public void All_ExpandsToEveryPlayerTheExecutorIncluded()
	{
		// Minecraft's meaning, which the console family follows (AGENTS.md rule 17): @a is every player, the one who typed
		// the command included. The old meaning — "the remote players" — is the reported "/heal @a does not heal me".
		var result = CommandSelectorResolver.Resolve("@a", [Local, Close, Far]);

		Assert.Equal([1UL, 10UL, 11UL], result);
	}

	[Fact]
	public void Entities_ExpandsToEveryPlayerBecauseTheDomainIsPlayers()
	{
		var result = CommandSelectorResolver.Resolve("@e", [Local, Close, Far]);

		Assert.Equal([1UL, 10UL, 11UL], result);
	}

	[Fact]
	public void Self_ExpandsToLocalPlayerOnly()
	{
		var result = CommandSelectorResolver.Resolve("@s", [Local, Close, Far]);

		Assert.Equal([1UL], result);
	}

	[Fact]
	public void Nearest_ReturnsTheExecutorBecauseItStandsAtDistanceZero()
	{
		// Minecraft again: @p is the nearest player, and the executor is at distance zero from itself. A test that means
		// "another player" says so with a filter — @p[distance=1..] — which is also what a player types.
		Assert.Equal([1UL], CommandSelectorResolver.Resolve("@p", [Local, Close, Far]));
		Assert.Equal([10UL], CommandSelectorResolver.Resolve("@p[distance=1..]", [Local, Close, Far]));
	}

	[Fact]
	public void Random_ReturnsOnePlayerOfEveryone()
	{
		var result = CommandSelectorResolver.Resolve("@r", [Local, Close, Far]);

		var single = Assert.Single(result);
		Assert.True(single == 1UL || single == 10UL || single == 11UL);
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

		Assert.Equal([1UL, 10UL, 11UL], result);
	}

	[Fact]
	public void AHostAlone_IsStillSelectableByEveryBaseSelectorThatMeansEveryone()
	{
		// The degenerate case the old shape made impossible: a solo host has no remote player, and @a used to answer
		// nothing, so the most convenient form of every cheat verb did nothing on the one client an operator debugs most.
		Assert.Equal([1UL], CommandSelectorResolver.Resolve("@a", [Local]));
		Assert.Equal([1UL], CommandSelectorResolver.Resolve("@p", [Local]));
		Assert.Equal([1UL], CommandSelectorResolver.Resolve("@r", [Local]));
		Assert.Equal([1UL], CommandSelectorResolver.Resolve("@e", [Local]));
		Assert.Empty(CommandSelectorResolver.Resolve("@a[distance=1..]", [Local]));
	}

	[Fact]
	public void TargetListPreservesInputOrderForAllSelectors()
	{
		var reversed = new CommandSelectorResolver.Target[] { Far, Close, Local };
		var result = CommandSelectorResolver.Resolve("@a", reversed);

		Assert.Equal([11UL, 10UL, 1UL], result);
	}

	[Fact]
	public void BracketedTypeFilter_AcceptsPlayerAndRejectsOtherTypes()
	{
		var withNames = new[] { Local, Close with { DisplayName = "Alice" }, Far with { DisplayName = "Bob" } };

		Assert.Equal([1UL, 10UL, 11UL], CommandSelectorResolver.Resolve("@a[type=player]", withNames));
		Assert.Equal([1UL, 10UL, 11UL], CommandSelectorResolver.Resolve("@a[type=cu:player]", withNames));
		Assert.Empty(CommandSelectorResolver.Resolve("@a[type=cuo:player]", withNames));
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
