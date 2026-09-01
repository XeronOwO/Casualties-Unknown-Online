using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

public class CommandSelectorFilterTests
{
	private static readonly CommandSelectorResolver.Target Local = new(1, true, new NetVector2(0f, 0f), "Host");
	private static readonly CommandSelectorResolver.Target Alice = new(10, false, new NetVector2(5f, 0f), "Alice");
	private static readonly CommandSelectorResolver.Target Bob = new(11, false, new NetVector2(20f, 0f), "Bob");

	[Fact]
	public void Parser_AcceptsKnownFilters()
	{
		Assert.True(CommandSelectorFilterParser.TryParse(
			"type=player,name=Alice,distance=4..10,limit=2,sort=nearest", out var filter));

		Assert.Equal("player", filter.Type);
		Assert.Equal("Alice", filter.Name);
		Assert.Equal(4f, filter.DistanceMin);
		Assert.Equal(10f, filter.DistanceMax);
		Assert.Equal(2, filter.Limit);
		Assert.Equal(SelectorSort.Nearest, filter.Sort);
	}

	[Fact]
	public void Parser_EmptyBodyIsNone()
	{
		Assert.True(CommandSelectorFilterParser.TryParse("", out var filter));
		Assert.Null(filter.Type);
		Assert.Null(filter.Name);
		Assert.Null(filter.DistanceMin);
		Assert.Null(filter.Limit);
	}

	[Fact]
	public void Parser_RejectsUnknownKey() =>
		Assert.False(CommandSelectorFilterParser.TryParse("type=player,unknown=1", out _));

	[Fact]
	public void Parser_RejectsMalformedPair() =>
		Assert.False(CommandSelectorFilterParser.TryParse("typeplayer", out _));

	[Fact]
	public void Parser_RejectsInvalidDistanceAndLimit()
	{
		Assert.False(CommandSelectorFilterParser.TryParse("distance=abc", out _));
		Assert.False(CommandSelectorFilterParser.TryParse("limit=0", out _));
		Assert.False(CommandSelectorFilterParser.TryParse("limit=-1", out _));
	}

	[Fact]
	public void TypeFilter_MatchesPlayersOnly()
	{
		var filter = CommandSelectorFilter.None with { Type = "player" };
		var zombie = CommandSelectorFilter.None with { Type = "zombie" };

		Assert.True(filter.Matches(Alice, new NetVector2(0f, 0f)));
		Assert.False(zombie.Matches(Alice, new NetVector2(0f, 0f)));
	}

	[Fact]
	public void NameFilter_IsCaseInsensitiveAndExact()
	{
		var filter = CommandSelectorFilter.None with { Name = "alice" };

		Assert.True(filter.Matches(Alice, new NetVector2(0f, 0f)));
		Assert.False(filter.Matches(Bob, new NetVector2(0f, 0f)));
	}

	[Fact]
	public void DistanceFilter_MatchesRange()
	{
		var filter = CommandSelectorFilter.None with { DistanceMin = 4f, DistanceMax = 10f };
		var exact = CommandSelectorFilter.None with { DistanceMin = 20f, DistanceMax = 20f };

		Assert.True(filter.Matches(Alice, new NetVector2(0f, 0f)));
		Assert.False(filter.Matches(Bob, new NetVector2(0f, 0f)));
		Assert.True(exact.Matches(Bob, new NetVector2(0f, 0f)));
		Assert.False(exact.Matches(Local, new NetVector2(0f, 0f)));
	}
}
