using System;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.PinyinSearch.Core;
using CasualtiesUnknownOnline.Runtime.Session.Content;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Tests.Fakes;
using CasualtiesUnknownOnline.Tests.Patching;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.PinyinSearch;

/// <summary>
/// The satellite mod's console half at the stage level: the pinyin → Chinese
/// display name → canonical id chain, the mod's OWN switch (off leaves the
/// built-in ranking untouched), the live-switch path and the boundary cases.
/// The matcher rows behind the crafting search box live in
/// <see cref="PinyinMatcherTests"/>; the production registration path (the
/// [CuoMod] entry point binding a loaded mod's context) is
/// <see cref="PinyinSearchModConsoleTests"/>.
///
/// The class joins the non-parallel collection because the mod's switch is
/// process-global state in the mod's own assembly — the static entry every
/// static patch class and this stage read.
/// </summary>
[Collection(GameAssemblyCollection.Name)]
public class PinyinSearchStageTests
{
	private static readonly ResourceLocationEntry Fentanyl =
		new(ContentId.Parse("cu:fentanyl"), ModContentKind.Item, "芬太尼");

	/// <summary>Binds the mod's switch to a throwaway config file — the entry every surface reads live.</summary>
	private static ConfigEntry<bool> Switch(bool enabled)
	{
		var path = Path.Combine(Path.GetTempPath(), "cuo-tests", $"pinyin-{Guid.NewGuid():N}.cfg");
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		return PinyinSearchConfig.Bind(new ConfigFile(path, saveOnInit: true), enabled);
	}

	private static PinyinSearchStage Stage(bool enabled)
	{
		Switch(enabled);
		return new PinyinSearchStage();
	}

	private static ResourceLocationCatalog Catalog(bool enabled, params ResourceLocationEntry[] entries) =>
		new(
			[new StubResourceSource(entries)],
			[Stage(enabled)],
			new ModResourceCompletionStore(),
			NullLogger<ResourceLocationCatalog>.Instance);

	[Theory]
	[InlineData("fent")]
	[InlineData("fentaini")]
	[InlineData("ftn")]
	[InlineData("芬太")]
	[InlineData("tai")]
	[InlineData("FENT")]
	public void Matches_ChineseDisplayName_ByFullPinyinInitialsMixedOrChinese(string query) =>
		Assert.True(Stage(true).Matches(Fentanyl, query), $"'{query}' must match 芬太尼");

	[Theory]
	[InlineData("fent")]
	[InlineData("ftn")]
	[InlineData("芬太")]
	[InlineData("cu:fent")]
	public void Suggest_ReachesTheCanonicalId_FromPinyinInitialsChineseAndIdPrefixes(string query)
	{
		var suggestion = Assert.Single(Catalog(true, Fentanyl).Suggest(query));

		Assert.Equal("cu:fentanyl", suggestion.Id.ToString());
		Assert.Equal("芬太尼", suggestion.DisplayName);
	}

	[Fact]
	public void Suggest_SwitchOff_LeavesTheNativeRankingUntouched()
	{
		var catalog = Catalog(false, Fentanyl);

		Assert.Empty(catalog.Suggest("ftn"));
		Assert.Equal(["cu:fentanyl"], catalog.Suggest("fent").Select(e => e.Id.ToString()));
		Assert.Equal(["cu:fentanyl"], catalog.Suggest("芬太").Select(e => e.Id.ToString()));
		Assert.Equal(["cu:fentanyl"], catalog.Suggest("cu:fent").Select(e => e.Id.ToString()));
	}

	[Fact]
	public void Suggest_FollowsTheSwitchWithoutRebuildingTheCatalog()
	{
		var switchEntry = Switch(false);
		var catalog = new ResourceLocationCatalog(
			[new StubResourceSource(Fentanyl)],
			[new PinyinSearchStage()],
			new ModResourceCompletionStore(),
			NullLogger<ResourceLocationCatalog>.Instance);

		Assert.Empty(catalog.Suggest("ftn"));

		switchEntry.Value = true;

		Assert.Equal(["cu:fentanyl"], catalog.Suggest("ftn").Select(e => e.Id.ToString()));
	}

	[Fact]
	public void Suggest_MatchesTheDisplayName_NeverTheIdString()
	{
		var idLooksLikePinyin = new ResourceLocationEntry(
			ContentId.Parse("cu:fentaini"), ModContentKind.Item, "Some English Name");
		var catalog = Catalog(true, idLooksLikePinyin);

		Assert.Empty(catalog.Suggest("ftn"));
		Assert.Equal(["cu:fentaini"], catalog.Suggest("fent").Select(e => e.Id.ToString()));
	}

	[Fact]
	public void Matches_AsciiDisplayName_StillMatchesByTheSharedLiteralRule()
	{
		var entry = new ResourceLocationEntry(ContentId.Parse("mymod:sword"), ModContentKind.Item, "Wooden Sword");

		Assert.True(Stage(true).Matches(entry, "ooden"));
		Assert.False(Stage(true).Matches(entry, "sword x"));
	}

	[Fact]
	public void Matches_EmptyDisplayNameOrEmptyPrefix_IsFalse()
	{
		var stage = Stage(true);
		var unnamed = new ResourceLocationEntry(ContentId.Parse("cu:thing"), ModContentKind.Item, "");

		Assert.False(stage.Matches(unnamed, "ftn"));
		Assert.False(stage.Matches(Fentanyl, ""));
	}

	[Fact]
	public void Suggest_UnrelatedQuery_ReturnsNothing() =>
		Assert.Empty(Catalog(true, Fentanyl).Suggest("zzzz"));
}
