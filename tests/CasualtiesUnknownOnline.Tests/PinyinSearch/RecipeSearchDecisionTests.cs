using CasualtiesUnknownOnline.PinyinSearch.Core.Search;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.PinyinSearch;

/// <summary>
/// The pure decision behind the crafting search scope — the matrix that decides
/// whether a native refresh extends its filter. This is the testable half of the
/// patch layer: the patch classes, the static gate and the BepInEx wiring run
/// only inside the game (the test project excludes the Game Adapter from
/// compilation), so the decision was deliberately kept in Runtime.
/// </summary>
public class RecipeSearchDecisionTests
{
	[Theory]
	[InlineData(true, "xws", false, "xws")] // on + typed + no item filter → extend
	[InlineData(false, "xws", false, null)] // switch off → native behavior
	[InlineData(true, "", false, null)] // nothing typed → native behavior
	[InlineData(true, null, false, null)] // nothing typed → native behavior
	[InlineData(true, "xws", true, null)] // an item filter replaces the text filter → native behavior
	[InlineData(false, "xws", true, null)] // both off → native behavior
	public void QueryFor_DecidesTheWholeMatrix(bool enabled, string? filter, bool itemFilterActive, string? expected) => Assert.Equal(expected, RecipeSearchDecision.QueryFor(enabled, filter, itemFilterActive));

	[Fact]
	public void QueryFor_ReturnsTheTypedQueryUnchanged() => Assert.Equal("xian维", RecipeSearchDecision.QueryFor(pinyinEnabled: true, "xian维", itemFilterActive: false));
}
