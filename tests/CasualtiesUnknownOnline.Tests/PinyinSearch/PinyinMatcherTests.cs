using CasualtiesUnknownOnline.PinyinSearch.Core.Search;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.PinyinSearch;

/// <summary>
/// The pinyin matcher itself: full pinyin, initials, mixed Chinese/pinyin
/// input, polyphonic characters, fuzzy sounds and tone tolerance, plus the
/// cases that must stay non-matches.
/// </summary>
public class PinyinMatcherTests
{
	[Theory]
	[InlineData("xianweisheng", true)]
	[InlineData("xws", true)]
	[InlineData("sheng", true)]
	[InlineData("纤wei", true)]
	[InlineData("xian维", true)]
	[InlineData("qianwei", true)]
	[InlineData("XWS", true)]
	[InlineData("XianWeiSheng", true)]
	[InlineData("xian1wei2sheng2", true)]
	[InlineData("wei", true)]
	[InlineData("zhangsan", false)]
	[InlineData("zws", false)]
	[InlineData("weiwei", false)]
	public void Contains_MatchesTheTicketExamplesAndTheirCounterexamples(string filter, bool expected)
	{
		Assert.True(
			PinyinMatcher.Contains("纤维绳", filter) == expected,
			$"fiber rope vs '{filter}' should be {expected}");
	}

	[Theory]
	[InlineData("zong", true)] // fuzzy initial: zh typed as z
	[InlineData("cong", false)] // c is never a zh reading
	public void Contains_ToleratesTheZFuzzyInitial(string filter, bool expected)
	{
		Assert.True(
			PinyinMatcher.Contains("中", filter) == expected,
			$"'中' vs '{filter}' should be {expected}");
	}

	[Theory]
	[InlineData("cang", true)] // fuzzy initial: ch typed as c
	public void Contains_ToleratesTheCFuzzyInitial(string filter, bool expected)
	{
		Assert.True(
			PinyinMatcher.Contains("长", filter) == expected,
			$"'长' vs '{filter}' should be {expected}");
	}

	[Theory]
	[InlineData("ming", true)]
	[InlineData("min", true)] // fuzzy final: ing typed as in
	public void Contains_ToleratesTheDroppedTrailingG(string filter, bool expected)
	{
		Assert.True(
			PinyinMatcher.Contains("明", filter) == expected,
			$"'明' vs '{filter}' should be {expected}");
	}

	[Theory]
	[InlineData("xin", true)]
	[InlineData("xing", true)] // fuzzy final: in typed as ing
	public void Contains_ToleratesTheAddedTrailingG(string filter, bool expected)
	{
		Assert.True(
			PinyinMatcher.Contains("心", filter) == expected,
			$"'心' vs '{filter}' should be {expected}");
	}

	[Theory]
	[InlineData("san", true)]
	[InlineData("shan", true)] // fuzzy initial: s/sh
	[InlineData("sang", true)] // fuzzy final: an/ang
	[InlineData("chan", false)]
	public void Contains_ToleratesFuzzySounds(string filter, bool expected)
	{
		Assert.True(
			PinyinMatcher.Contains("三", filter) == expected,
			$"three vs '{filter}' should be {expected}");
	}

	[Theory]
	[InlineData("sheng", true)]
	[InlineData("seng", true)] // fuzzy initial, reverse direction: sh typed as s
	[InlineData("shen", true)] // fuzzy final: eng/en
	[InlineData("sheng1", true)]
	public void Contains_ToleratesFuzzySoundsOnATwoLetterInitial(string filter, bool expected)
	{
		Assert.True(
			PinyinMatcher.Contains("生", filter) == expected,
			$"'生' vs '{filter}' should be {expected}");
	}

	[Theory]
	[InlineData("中", "zhong", true)]
	[InlineData("中", "zhong4", true)]
	[InlineData("中", "zhong2", false)]
	[InlineData("长", "chang", true)]
	[InlineData("长", "zhang", true)]
	public void Contains_MatchesEveryReadingOfAPolyphonicCharacter(string name, string filter, bool expected)
	{
		Assert.True(
			PinyinMatcher.Contains(name, filter) == expected,
			$"'{name}' vs '{filter}' should be {expected}");
	}

	[Fact]
	public void Contains_EmptyFilter_MatchesEverything() => Assert.True(PinyinMatcher.Contains("纤维绳", string.Empty));

	[Fact]
	public void Contains_EmptyName_HasNoMatch() => Assert.False(PinyinMatcher.Contains(string.Empty, "xws"));

	[Fact]
	public void Contains_PlainAsciiNameIsMatchedLiterally()
	{
		Assert.True(PinyinMatcher.Contains("Wooden Sword", "wood"));
		Assert.False(PinyinMatcher.Contains("Wooden Sword", "xws"));
	}
}
