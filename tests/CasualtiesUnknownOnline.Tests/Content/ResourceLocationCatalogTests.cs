using System;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Content;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.Logging;
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
		new(sources, [], new ModResourceCompletionStore(), NullLogger<ResourceLocationCatalog>.Instance);

	private static ResourceLocationCatalog CreateCatalogWithStages(
		IResourceLocationSource source, params IResourceLocationMatchStage[] stages) =>
		new([source], stages, new ModResourceCompletionStore(), NullLogger<ResourceLocationCatalog>.Instance);

	private static ResourceLocationEntry Entry(string id, string kind = ModContentKind.Item, string displayName = "") =>
		new(ContentId.Parse(id), kind, string.IsNullOrEmpty(displayName) ? id : displayName);

	private static StubResourceSource Source(params ResourceLocationEntry[] entries) => new(entries);

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
			new StubMatchStage((entry, _) => entry.Id.Path == "zzz"));

		var suggestions = catalog.Suggest(query).Select(e => e.Id.ToString()).ToList();

		Assert.Equal([builtInId, "cu:zzz"], suggestions);
	}

	[Fact]
	public void Suggest_ExtraStages_RankInRegistrationOrder()
	{
		var catalog = CreateCatalogWithStages(
			Source(Entry("cu:aaa", displayName: "None"), Entry("cu:bbb", displayName: "None")),
			new StubMatchStage((entry, _) => entry.Id.Path == "bbb"),
			new StubMatchStage((entry, _) => entry.Id.Path == "aaa"));

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
		var catalog = CreateCatalogWithStages(Source(entries), new StubMatchStage((_, _) => true));

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
		var catalog = CreateCatalogWithStages(Source([.. stageOnly, .. builtIn]), new StubMatchStage((_, _) => true));

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
			new StubMatchStage((_, _) =>
			{
				consulted = true;
				return true;
			}));

		Assert.Equal(["cu:aaa", "cu:bbb"], catalog.Suggest("").Select(e => e.Id.ToString()));
		Assert.False(consulted, "an empty prefix takes the catalog's own first-entries path");
	}

	[Fact]
	public void Suggest_ThrowingFrameworkStage_CountsAsNoMatchAndTheLaterStageStillMatches()
	{
		var log = new RecordingLogger<ResourceLocationCatalog>();
		var catalog = new ResourceLocationCatalog(
			[Source(
				Entry("cu:aaa", displayName: "None"),
				Entry("cu:bbb", displayName: "None"),
				Entry("cu:ccc", displayName: "None"))],
			[
				new StubMatchStage((entry, _) => entry.Id.Path == "bbb"
					? throw new InvalidOperationException("stage failure")
					: entry.Id.Path == "ccc"),
				new StubMatchStage((entry, _) => entry.Id.Path == "bbb")
			],
			new ModResourceCompletionStore(),
			log);

		// "bbb" is reachable only through the SECOND stage, so the throwing first
		// stage was treated as "no match" rather than aborting the entry, and
		// "ccc" — the throwing stage's own match — keeps the rank it earned.
		Assert.Equal(["cu:ccc", "cu:bbb"], catalog.Suggest("zz").Select(e => e.Id.ToString()));
		Assert.Contains(
			log.Entries,
			entry => entry.Level == LogLevel.Debug
				&& entry.Message.Contains("treated as no match", StringComparison.Ordinal));
		Assert.DoesNotContain(log.Entries, entry => entry.Level >= LogLevel.Warning);
	}

	[Fact]
	public void Suggest_StageThatRegistersDuringAQuery_SeesTheNextQueryNotTheRunningOne()
	{
		var modStages = new ModResourceCompletionStore();
		var added = false;
		var catalog = new ResourceLocationCatalog(
			[Source(Entry("cu:aaa", displayName: "None"))],
			[
				new StubMatchStage((_, _) =>
				{
					if (!added)
					{
						added = true;
						modStages.Add(new StubMatchStage((entry, _) => entry.Id.Path == "aaa"));
					}

					return false;
				})
			],
			modStages,
			NullLogger<ResourceLocationCatalog>.Instance);

		// The running query ranks the table it started with — no match, and no
		// failure on the collection the stage just modified...
		Assert.Empty(catalog.Suggest("zz"));
		// ...and the next query consults the stage that was added.
		Assert.Equal(["cu:aaa"], catalog.Suggest("zz").Select(e => e.Id.ToString()));
	}

	[Fact]
	public void Suggest_ThrowingModStage_CountsAsNoMatchAndTheLaterStagesStillRun()
	{
		var modStages = new ModResourceCompletionStore();
		var catalog = new ResourceLocationCatalog(
			[Source(
				Entry("cu:aaa", displayName: "None"),
				Entry("cu:bbb", displayName: "None"),
				Entry("cu:ccc", displayName: "None"))],
			[new StubMatchStage((entry, _) => entry.Id.Path == "aaa")],
			modStages,
			NullLogger<ResourceLocationCatalog>.Instance);
		modStages.Add(new StubMatchStage((entry, _) => entry.Id.Path == "bbb"
			? throw new InvalidOperationException("mod stage failure")
			: entry.Id.Path == "ccc"));
		modStages.Add(new StubMatchStage((entry, _) => entry.Id.Path == "bbb"));

		// Rank order: the framework stage, then the two mod stages in
		// registration order. "bbb" is only reachable through the LAST stage,
		// which proves the throwing stage counted as no match instead of
		// aborting the query.
		Assert.Equal(["cu:aaa", "cu:ccc", "cu:bbb"], catalog.Suggest("zz").Select(e => e.Id.ToString()));
	}
}
