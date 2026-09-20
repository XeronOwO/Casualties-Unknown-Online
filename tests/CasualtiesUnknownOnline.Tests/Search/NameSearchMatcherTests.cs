using CasualtiesUnknownOnline.Runtime.Search;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Search;

/// <summary>
/// The shared search predicate: the native literal rule stays first and intact,
/// and the pinyin half is purely additive. Every surface that turns pinyin
/// search on must behave exactly like the native search when the query is
/// literal, empty or ASCII.
/// </summary>
public class NameSearchMatcherTests
{
	[Theory]
	[InlineData("纤维绳", "纤维", true)]
	[InlineData("纤维绳", "xws", true)]
	[InlineData("纤维绳", "xianweisheng", true)]
	[InlineData("纤维绳", "fiber", false)]
	[InlineData("Wooden Sword", "wood", true)]
	[InlineData("Wooden Sword", "WOODEN", true)]
	[InlineData("Wooden Sword", "xws", false)]
	public void Matches_ExtendsTheLiteralRuleWithPinyin(string name, string query, bool expected)
	{
		Assert.True(
			NameSearchMatcher.Matches(name, query) == expected,
			$"'{name}' vs '{query}' should be {expected}");
	}

	[Fact]
	public void Matches_EmptyQuery_MatchesEverything() => Assert.True(NameSearchMatcher.Matches("纤维绳", string.Empty));

	[Fact]
	public void Matches_EmptyName_HasNoMatch() => Assert.False(NameSearchMatcher.Matches(string.Empty, "xws"));
}
