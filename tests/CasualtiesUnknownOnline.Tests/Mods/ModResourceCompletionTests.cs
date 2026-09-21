using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Content;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Mods;

/// <summary>
/// The per-mod resource-completion stage registry — the console's completion
/// extension point as a mod-facing contract. These tests drive the adapter
/// directly (two mods over one store and one catalog), because the claims under
/// test are the per-mod scoping, the registration rails and what the catalog
/// does with a registered stage; the discovery path that builds one adapter per
/// mod is covered by the lifecycle tests.
/// </summary>
public class ModResourceCompletionTests
{
	private static ResourceLocationEntry Fentanyl =>
		new(ContentId.Parse("cu:fentanyl"), ModContentKind.Item, "芬太尼");

	private static (ResourceLocationCatalog Catalog, ModResourceCompletionStore Stages) NewCatalog()
	{
		var stages = new ModResourceCompletionStore();
		var catalog = new ResourceLocationCatalog(
			[new StubResourceSource(Fentanyl)],
			[],
			stages,
			NullLogger<ResourceLocationCatalog>.Instance);
		return (catalog, stages);
	}

	private static ModResourceCompletionAdapter ModOf(ModResourceCompletionStore stages, string modId) =>
		new(stages, new ModManifest(modId, modId, "1.0.0", NetworkMode.Synchronized, null), NullLogger.Instance);

	[Fact]
	public void Register_ReachesTheCatalog_HasReports_FreeAndUnregisterTakesItBack()
	{
		var (catalog, stages) = NewCatalog();
		var completion = ModOf(stages, "mod.a");

		Assert.Equal(0, completion.MatchStageCount);
		Assert.Empty(completion.MatchStageIds);
		Assert.Empty(catalog.Suggest("ftn")); // the built-in ranks cannot complete pinyin

		Assert.True(completion.TryRegisterMatchStage(
			"pinyin",
			new StubMatchStage((entry, prefix) => entry.DisplayName == "芬太尼" && prefix == "ftn")));
		Assert.True(completion.IsMatchStageRegistered("pinyin"));
		Assert.Equal(1, completion.MatchStageCount);
		Assert.Equal(["pinyin"], completion.MatchStageIds);
		Assert.Equal(["cu:fentanyl"], catalog.Suggest("ftn").Select(e => e.Id.ToString()));

		Assert.True(completion.TryUnregisterMatchStage("pinyin"));
		Assert.False(completion.IsMatchStageRegistered("pinyin"));
		Assert.Equal(0, completion.MatchStageCount);
		Assert.Empty(completion.MatchStageIds);
		Assert.Empty(catalog.Suggest("ftn"));
		Assert.False(completion.TryUnregisterMatchStage("pinyin"));
	}

	[Fact]
	public void StageTables_AreScopedToTheRegisteringMod()
	{
		var (catalog, stages) = NewCatalog();
		var first = ModOf(stages, "mod.a");
		var second = ModOf(stages, "mod.b");

		// The same id in two mods is two registrations, not a duplicate.
		Assert.True(first.TryRegisterMatchStage("pinyin", new StubMatchStage((_, prefix) => prefix == "aaa")));
		Assert.True(second.TryRegisterMatchStage("pinyin", new StubMatchStage((_, prefix) => prefix == "bbb")));
		Assert.True(first.IsMatchStageRegistered("pinyin"));
		Assert.True(second.IsMatchStageRegistered("pinyin"));
		Assert.Equal(1, first.MatchStageCount);
		Assert.Equal(1, second.MatchStageCount);
		Assert.Equal(["cu:fentanyl"], catalog.Suggest("aaa").Select(e => e.Id.ToString()));
		Assert.Equal(["cu:fentanyl"], catalog.Suggest("bbb").Select(e => e.Id.ToString()));

		// Unregistering on one mod never touches the other mod's stage.
		Assert.True(first.TryUnregisterMatchStage("pinyin"));
		Assert.False(first.IsMatchStageRegistered("pinyin"));
		Assert.True(second.IsMatchStageRegistered("pinyin"));
		Assert.Empty(catalog.Suggest("aaa"));
		Assert.Equal(["cu:fentanyl"], catalog.Suggest("bbb").Select(e => e.Id.ToString()));
	}

	[Fact]
	public void Register_RejectsNullInvalidAndDuplicateIds()
	{
		var (_, stages) = NewCatalog();
		var completion = ModOf(stages, "mod.a");

		Assert.False(completion.TryRegisterMatchStage("pinyin", null!));
		Assert.False(completion.TryRegisterMatchStage("", new StubMatchStage((_, _) => true)));
		Assert.False(completion.TryRegisterMatchStage("   ", new StubMatchStage((_, _) => true)));
		Assert.False(completion.TryRegisterMatchStage(
			new string('x', ModResourceCompletionAdapter.MaxMatchStageIdLength + 1),
			new StubMatchStage((_, _) => true)));
		Assert.Equal(0, completion.MatchStageCount);
		Assert.Empty(stages.Stages);

		Assert.True(completion.TryRegisterMatchStage("pinyin", new StubMatchStage((_, _) => true)));
		Assert.False(completion.TryRegisterMatchStage("pinyin", new StubMatchStage((_, _) => true)));
		Assert.Equal(1, completion.MatchStageCount);
	}

	[Fact]
	public void Register_RefusesPastThePerModCap_AndAFreedSlotIsUsableAgain()
	{
		var (_, stages) = NewCatalog();
		var completion = ModOf(stages, "mod.a");
		for (var index = 0; index < ModResourceCompletionAdapter.MaxMatchStagesPerMod; index++)
		{
			Assert.True(completion.TryRegisterMatchStage($"stage-{index}", new StubMatchStage((_, _) => true)));
		}

		Assert.Equal(ModResourceCompletionAdapter.MaxMatchStagesPerMod, completion.MatchStageCount);
		Assert.False(completion.TryRegisterMatchStage("one-too-many", new StubMatchStage((_, _) => true)));
		Assert.Equal(ModResourceCompletionAdapter.MaxMatchStagesPerMod, completion.MatchStageCount);
		Assert.Equal(ModResourceCompletionAdapter.MaxMatchStagesPerMod, stages.Stages.Count);

		Assert.True(completion.TryUnregisterMatchStage("stage-0"));
		Assert.True(completion.TryRegisterMatchStage("one-too-many", new StubMatchStage((_, _) => true)));
		Assert.Equal(ModResourceCompletionAdapter.MaxMatchStagesPerMod, completion.MatchStageCount);
	}
}
