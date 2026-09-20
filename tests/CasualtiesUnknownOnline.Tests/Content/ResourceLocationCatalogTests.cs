using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Content;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Content;

/// <summary>
/// The console's resource vocabulary catalog: it merges every source by
/// canonical id and owns the completion matching/ranking contract — the console
/// itself must never invent a suggestion text.
/// </summary>
public class ResourceLocationCatalogTests
{
	private static ResourceLocationCatalog CreateCatalog(params IResourceLocationSource[] sources) =>
		new(sources, [], NullLogger<ResourceLocationCatalog>.Instance);

	private static ResourceLocationCatalog CreateCatalogWithStages(
		IResourceLocationSource source, params IResourceLocationMatchStage[] stages) =>
		new([source], stages, NullLogger<ResourceLocationCatalog>.Instance);

	private static ResourceLocationEntry Entry(string id, string kind = ModContentKind.Item, string displayName = "") =>
		new(ContentId.Parse(id), kind, string.IsNullOrEmpty(displayName) ? id : displayName);

	private static StubSource Source(params ResourceLocationEntry[] entries) => new(entries);

	[Fact]
	public void Entries_MergeSourcesAndDeduplicateByCanonicalId()
	{
		var catalog = CreateCatalog(
			Source(Entry("cu:fentanyl", displayName: "芬太尼"), Entry("cu:bandage", displayName: "绷带")),
			Source(Entry("cu:fentanyl", displayName: "duplicate loser"), Entry("mymod:sword", displayName: "Wooden Sword")));

		var entries = catalog.Entries;

		Assert.Equal(3, entries.Count);
		Assert.Equal("芬太尼", entries.Single(e => e.Id.ToString() == "cu:fentanyl").DisplayName);
		Assert.Equal(["cu:bandage", "cu:fentanyl", "mymod:sword"], entries.Select(e => e.Id.ToString()));
	}

	[Fact]
	public void Suggest_MatchesBarePathCanonicalPrefixAndDisplayName()
	{
		var catalog = CreateCatalog(Source(
			Entry("cu:fentanyl", displayName: "芬太尼"),
			Entry("mymod:wooden.sword", displayName: "Wooden Sword")));

		Assert.Equal(["cu:fentanyl"], catalog.Suggest("fen").Select(e => e.Id.ToString()));
		Assert.Equal(["cu:fentanyl"], catalog.Suggest("cu:fen").Select(e => e.Id.ToString()));
		Assert.Equal(["cu:fentanyl"], catalog.Suggest("芬太").Select(e => e.Id.ToString()));
		Assert.Equal(["cu:fentanyl"], catalog.Suggest("FEN").Select(e => e.Id.ToString()));
		Assert.Equal(["mymod:wooden.sword"], catalog.Suggest("wo").Select(e => e.Id.ToString()));
		Assert.Equal(["mymod:wooden.sword"], catalog.Suggest("wooden").Select(e => e.Id.ToString()));
		Assert.Equal(["mymod:wooden.sword"], catalog.Suggest("mymod:wo").Select(e => e.Id.ToString()));
		Assert.Equal(["mymod:wooden.sword"], catalog.Suggest("wooden sword").Select(e => e.Id.ToString()));
	}

	[Fact]
	public void Suggest_ReturnsCanonicalIdNeverTheInputAlias()
	{
		var catalog = CreateCatalog(Source(Entry("cu:fentanyl", displayName: "芬太尼")));

		foreach (var prefix in new[] { "fen", "cu:fen", "芬太", "FEN" })
		{
			var suggestion = Assert.Single(catalog.Suggest(prefix));
			Assert.Equal("cu:fentanyl", suggestion.Id.ToString());
			Assert.NotEqual(prefix, suggestion.Id.ToString());
			Assert.Equal("芬太尼", suggestion.DisplayName);
		}
	}

	[Fact]
	public void Suggest_ExactCanonicalMatchRanksBeforePrefixAndNameMatches()
	{
		var catalog = CreateCatalog(Source(
			Entry("cu:fen", displayName: "Fen Something"),
			Entry("cu:fentanyl", displayName: "Fen"),
			Entry("cu:bandage", displayName: "cu:fen")));

		var suggestions = catalog.Suggest("cu:fen").Select(e => e.Id.ToString()).ToList();

		Assert.Equal("cu:fen", suggestions[0]);
		Assert.Contains("cu:fentanyl", suggestions);
	}

	[Fact]
	public void Suggest_PathPrefixRanksBeforeDisplayNamePrefix()
	{
		var catalog = CreateCatalog(Source(
			Entry("cu:bandage", displayName: "Unrelated"),
			Entry("cu:aaa", displayName: "Bandage Box")));

		var suggestions = catalog.Suggest("ban").Select(e => e.Id.ToString()).ToList();

		Assert.Equal(["cu:bandage", "cu:aaa"], suggestions);
	}

	[Fact]
	public void Suggest_IsCappedAndDeterministic()
	{
		var entries = Enumerable.Range(0, ResourceLocationCatalog.MaxSuggestions + 5)
			.Select(i => Entry($"cu:item{i:D2}"))
			.ToArray();
		var catalog = CreateCatalog(Source(entries));

		var first = catalog.Suggest("cu:").Select(e => e.Id.ToString()).ToList();
		var second = catalog.Suggest("cu:").Select(e => e.Id.ToString()).ToList();

		Assert.Equal(ResourceLocationCatalog.MaxSuggestions, first.Count);
		Assert.Equal(first, second);
		Assert.Equal("cu:item00", first[0]);
		Assert.Equal($"cu:item{ResourceLocationCatalog.MaxSuggestions - 1:D2}", first[first.Count - 1]);
	}

	[Fact]
	public void Suggest_EmptyOrNullPrefix_ReturnsFirstEntriesInIdOrder()
	{
		var catalog = CreateCatalog(Source(Entry("cu:bandage"), Entry("cu:fentanyl")));

		Assert.Equal(["cu:bandage", "cu:fentanyl"], catalog.Suggest("").Select(e => e.Id.ToString()));
		Assert.Equal(["cu:bandage", "cu:fentanyl"], catalog.Suggest(null!).Select(e => e.Id.ToString()));
		Assert.Equal(["cu:bandage", "cu:fentanyl"], catalog.Suggest("   ").Select(e => e.Id.ToString()));
	}

	[Fact]
	public void Suggest_NoMatch_ReturnsEmpty()
	{
		var catalog = CreateCatalog(Source(Entry("cu:bandage")));

		Assert.Empty(catalog.Suggest("zzz"));
		Assert.Empty(CreateCatalog().Suggest("cu:"));
		Assert.Empty(CreateCatalog().Entries);
	}

	[Fact]
	public void Entries_SkipUninitialisedIdsFromSources()
	{
		var catalog = CreateCatalog(Source(
			new ResourceLocationEntry(default, ModContentKind.Item, "broken"),
			Entry("cu:bandage")));

		var entry = Assert.Single(catalog.Entries);
		Assert.Equal("cu:bandage", entry.Id.ToString());
		Assert.Single(catalog.Suggest("cu:"));
	}

	[Theory]
	[InlineData("cu:exact", "cu:exact")]
	[InlineData("cu:ex", "cu:exact")]
	[InlineData("fenp", "cu:fenpath")]
	[InlineData("芬太", "cu:name")]
	public void Suggest_ExtraStageMatches_RankAfterEveryBuiltInRank(string query, string builtInId)
	{
		var catalog = CreateCatalogWithStages(
			Source(
				Entry("cu:exact", displayName: "Unrelated"),
				Entry("cu:fenpath", displayName: "Unrelated"),
				Entry("cu:name", displayName: "芬太尼"),
				Entry("cu:zzz", displayName: "Stage only")),
			new StubStage((entry, _) => entry.Id.Path == "zzz"));

		var suggestions = catalog.Suggest(query).Select(e => e.Id.ToString()).ToList();

		Assert.Equal([builtInId, "cu:zzz"], suggestions);
	}

	[Fact]
	public void Suggest_ExtraStages_RankInRegistrationOrder()
	{
		var catalog = CreateCatalogWithStages(
			Source(Entry("cu:aaa", displayName: "None"), Entry("cu:bbb", displayName: "None")),
			new StubStage((entry, _) => entry.Id.Path == "bbb"),
			new StubStage((entry, _) => entry.Id.Path == "aaa"));

		var suggestions = catalog.Suggest("zz").Select(e => e.Id.ToString()).ToList();

		Assert.Equal(["cu:bbb", "cu:aaa"], suggestions);
	}

	[Fact]
	public void Suggest_ExtraStageMatches_AreOrderedByIdWithinTheStageAndStillCapped()
	{
		// The source order is deliberately the reverse of the id order: the
		// result must come back ordered by canonical id, not by source position.
		var entries = Enumerable.Range(0, ResourceLocationCatalog.MaxSuggestions + 5)
			.Select(i => Entry($"cu:item{i:D2}"))
			.Reverse()
			.ToArray();
		var catalog = CreateCatalogWithStages(Source(entries), new StubStage((_, _) => true));

		var first = catalog.Suggest("zz").Select(e => e.Id.ToString()).ToList();
		var second = catalog.Suggest("zz").Select(e => e.Id.ToString()).ToList();

		Assert.Equal(ResourceLocationCatalog.MaxSuggestions, first.Count);
		Assert.Equal(first, second);
		Assert.Equal("cu:item00", first[0]);
		Assert.Equal($"cu:item{ResourceLocationCatalog.MaxSuggestions - 1:D2}", first[first.Count - 1]);
	}

	[Fact]
	public void Suggest_ExtraStageThatMatchesEverything_CannotCrowdBuiltInMatchesOutOfTheCap()
	{
		var stageOnly = Enumerable.Range(0, ResourceLocationCatalog.MaxSuggestions + 5)
			.Select(i => Entry($"cu:aaa{i:D2}"))
			.ToArray();
		var builtIn = Enumerable.Range(0, ResourceLocationCatalog.MaxSuggestions + 5)
			.Select(i => Entry($"cu:item{i:D2}"))
			.ToArray();
		var catalog = CreateCatalogWithStages(Source([.. stageOnly, .. builtIn]), new StubStage((_, _) => true));

		var suggestions = catalog.Suggest("cu:item").Select(e => e.Id.ToString()).ToList();

		Assert.Equal(ResourceLocationCatalog.MaxSuggestions, suggestions.Count);
		Assert.All(suggestions, id => Assert.StartsWith("cu:item", id, StringComparison.Ordinal));
	}

	[Fact]
	public void Suggest_WithoutMatchStages_KeepsTheBuiltInResultsExactly()
	{
		var catalog = CreateCatalog(Source(Entry("cu:fentanyl", displayName: "芬太尼")));

		Assert.Equal(["cu:fentanyl"], catalog.Suggest("fen").Select(e => e.Id.ToString()));
		Assert.Equal(["cu:fentanyl"], catalog.Suggest("芬太").Select(e => e.Id.ToString()));
		Assert.Empty(catalog.Suggest("ftn"));
	}

	[Fact]
	public void Suggest_EmptyPrefix_StillTakesTheFirstEntriesPath_AndNeverConsultsStages()
	{
		var consulted = false;
		var catalog = CreateCatalogWithStages(
			Source(Entry("cu:aaa"), Entry("cu:bbb")),
			new StubStage((_, _) =>
			{
				consulted = true;
				return true;
			}));

		Assert.Equal(["cu:aaa", "cu:bbb"], catalog.Suggest("").Select(e => e.Id.ToString()));
		Assert.False(consulted, "an empty prefix takes the catalog's own first-entries path");
	}

	private sealed class StubStage(Func<ResourceLocationEntry, string, bool> matches) : IResourceLocationMatchStage
	{
		public bool Matches(ResourceLocationEntry entry, string prefix) => matches(entry, prefix);
	}

	private sealed class StubSource(params ResourceLocationEntry[] entries) : IResourceLocationSource
	{
		public IReadOnlyList<ResourceLocationEntry> Entries => entries;
	}
}
