using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Session.Content;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Content;

/// <summary>
/// The console's pinyin completion stage (ticket stage 2): the acceptance chain
/// pinyin input → Chinese display name → canonical id, plus the switch-off,
/// boundary and live-switch paths. The catalog's own tests cover the pluggable
/// ranking; these cover what the stage itself claims.
/// </summary>
public class PinyinResourceLocationMatchStageTests
{
	private static readonly ResourceLocationEntry Fentanyl =
		new(ContentId.Parse("cu:fentanyl"), ModContentKind.Item, "芬太尼");

	private static MutableOptionsMonitor<PinyinSearchOptions> Monitor(bool enabled) =>
		new(new PinyinSearchOptions { Enabled = enabled });

	private static PinyinResourceLocationMatchStage Stage(MutableOptionsMonitor<PinyinSearchOptions> monitor) =>
		new(monitor, NullLogger<PinyinResourceLocationMatchStage>.Instance);

	private static ResourceLocationCatalog Catalog(bool enabled, params ResourceLocationEntry[] entries) =>
		new(
			[new StubSource(entries)],
			[Stage(Monitor(enabled))],
			NullLogger<ResourceLocationCatalog>.Instance);

	[Theory]
	[InlineData("fent")]
	[InlineData("fentaini")]
	[InlineData("ftn")]
	[InlineData("芬太")]
	[InlineData("tai")]
	[InlineData("FENT")]
	public void Matches_ChineseDisplayName_ByFullPinyinInitialsMixedOrChinese(string query) =>
		Assert.True(Stage(Monitor(true)).Matches(Fentanyl, query), $"'{query}' must match 芬太尼");

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
		var monitor = Monitor(false);
		var catalog = new ResourceLocationCatalog(
			[new StubSource(Fentanyl)],
			[Stage(monitor)],
			NullLogger<ResourceLocationCatalog>.Instance);

		Assert.Empty(catalog.Suggest("ftn"));

		monitor.Set(new PinyinSearchOptions { Enabled = true });

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

		Assert.True(Stage(Monitor(true)).Matches(entry, "ooden"));
		Assert.False(Stage(Monitor(true)).Matches(entry, "sword x"));
	}

	[Fact]
	public void Matches_EmptyDisplayNameOrEmptyPrefix_IsFalse()
	{
		var stage = Stage(Monitor(true));
		var unnamed = new ResourceLocationEntry(ContentId.Parse("cu:thing"), ModContentKind.Item, "");

		Assert.False(stage.Matches(unnamed, "ftn"));
		Assert.False(stage.Matches(Fentanyl, ""));
	}

	[Fact]
	public void Suggest_UnrelatedQuery_ReturnsNothing() =>
		Assert.Empty(Catalog(true, Fentanyl).Suggest("zzzz"));

	private sealed class StubSource(params ResourceLocationEntry[] entries) : IResourceLocationSource
	{
		public IReadOnlyList<ResourceLocationEntry> Entries => entries;
	}
}
