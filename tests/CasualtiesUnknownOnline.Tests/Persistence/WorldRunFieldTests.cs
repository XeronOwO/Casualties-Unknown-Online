using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CasualtiesUnknownOnline.GameState.Domains.World;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Session.Persistence;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// S3.4's run-level parity: the native values a continued run needs that no CUO
/// domain owns. The kernel's run baseline already carries the generation random
/// state and the run settings; these tests pin the other half — the two accumulated
/// rarity multipliers (stamped into the baseline, so a side that GENERATES the
/// layer uses them) and the run clock base plus the recipe unlock table (their own
/// typed row), and the rule that a reader which cannot read them refuses the cut
/// instead of storing defaults.
/// </summary>
public sealed class WorldRunFieldTests
{
	private const ulong HostId = 1001UL;

	[Fact]
	public void MidRunCut_WritesTheRunBaselineAndTheNativeRunFields()
	{
		var native = new FakeNativeWorldFacts();
		native.SeedRunFields(2.5f, 3.5f, 42.5f, Recipe(0, madeBefore: true, intValue: 0), Recipe(3, madeBefore: false, intValue: 7));
		native.SeedLayerTime(366.5f);

		using var fixture = WorldSaveFixture.Create("run-fields-midrun", nativeWorldFacts: native);
		Assert.True(fixture.Service.TryBeginRun(isTutorial: false));
		Assert.True(fixture.Kernel.TryStartRun(HostId, Run(layerIndex: 2, loot: 2.5f, trap: 3.5f), out _, out _));

		var report = Cut(fixture);

		Assert.True(report.Captured, report.Summary);
		var rows = LiveRunRows(fixture);
		var baseline = rows.Single(row => KindOf(row) == SaveRunRow.RunKind).GetProperty("run");

		// The multipliers are the run baseline's own generation-boundary values (the
		// value the named layer was generated with) — the cut does not re-stamp them.
		Assert.Equal(2.5f, baseline.GetProperty("lootRarityMultiplier").GetSingle());
		Assert.Equal(3.5f, baseline.GetProperty("trapRarityMultiplier").GetSingle());

		var fields = rows.Single(row => KindOf(row) == SaveRunRow.NativeRunFieldsKind).GetProperty("nativeRunFields");
		Assert.Equal(42.5f, fields.GetProperty("savedRunTime").GetSingle());
		// The layer's own countdown rides the run row too, so a continued layer resumes the
		// radiation line instead of restarting it (the native continue restarts it).
		Assert.Equal(366.5f, fields.GetProperty("layerTimeSpent").GetSingle());
		var recipes = fields.GetProperty("recipes").EnumerateArray().ToList();
		Assert.Equal(2, recipes.Count);
		Assert.Equal(0, recipes[0].GetProperty("index").GetInt32());
		Assert.True(recipes[0].GetProperty("madeBefore").GetBoolean());
		Assert.Equal(3, recipes[1].GetProperty("index").GetInt32());
		Assert.Equal(7, recipes[1].GetProperty("intValue").GetInt32());
	}

	[Fact]
	public void MidRunCut_WithNoLayerTimerRead_WritesNoLayerTimeProperty()
	{
		// The layer timer is OPTIONAL in the row, and its absence must stay an absence: a
		// reader that met a world which could not report one must not write a zero, which
		// would be read back as "the layer has just started".
		var native = new FakeNativeWorldFacts();
		native.SeedRunFields(1f, 1f, 20f);
		native.SeedLayerTime(null);

		using var fixture = WorldSaveFixture.Create("run-fields-no-layer-time", nativeWorldFacts: native);
		Assert.True(fixture.Service.TryBeginRun(isTutorial: false));
		Assert.True(fixture.Kernel.TryStartRun(HostId, Run(layerIndex: 1), out _, out _));

		Assert.True(Cut(fixture).Captured);

		var fields = LiveRunRows(fixture).Single(row => KindOf(row) == SaveRunRow.NativeRunFieldsKind).GetProperty("nativeRunFields");
		Assert.False(fields.TryGetProperty("layerTimeSpent", out _), "an unread layer timer must not be recorded as a zero");
	}

	[Fact]
	public void Continue_CarriesTheLayerTimerBackToTheNativeApplier()
	{
		var cutNative = new FakeNativeWorldFacts();
		cutNative.SeedRunFields(1f, 1f, 120f, Recipe(0, madeBefore: true, intValue: 0));
		cutNative.SeedLayerTime(410.5f);

		using var fixture = WorldSaveFixture.Create("run-fields-layer-resume", nativeWorldFacts: cutNative);
		Assert.True(fixture.Service.TryBeginRun(isTutorial: false));
		Assert.True(fixture.Kernel.TryStartRun(HostId, Run(layerIndex: 1), out _, out _));
		Assert.True(Cut(fixture).Captured);
		Assert.True(fixture.Repository.Repository.SetLastOpenedWorld(fixture.WorldId));

		var restoreNative = new FakeNativeWorldFacts();
		using var restarted = WorldSaveFixture.Create("run-fields-layer-resume-restart", repository: fixture.Repository, nativeWorldFacts: restoreNative);

		Assert.True(restarted.Service.TryContinue(out var outcome), outcome.Summary);

		// The layer time travels with the clock, and it is written by the same world-entry
		// flush the clock uses — the layer the player continues into resumes its countdown.
		// The handover itself arms a pending write (the world does not exist at the click), so
		// this drives the seam the adapter drives: TryWritePendingRunFields.
		Assert.Contains("apply-cut-run-fields", restoreNative.Calls);
		Assert.Equal(120f, restoreNative.RunFields.SavedRunTime);
		Assert.True(restoreNative.HasPendingClockFacts, "the layer timer a restore handed over must be waiting for the live world");
		Assert.True(restoreNative.TryWritePendingRunFields(), "the world-entry flush must take the handed-over values");
		Assert.Equal(410.5f, restoreNative.RunFields.LayerTimeSpent);
		Assert.Contains(restoreNative.ClockWrites, write => write.StartsWith("layer-time 410.5", StringComparison.Ordinal));
	}

	[Fact]
	public void Continue_FromAnArchiveWithoutTheLayerTime_KeepsTheLiveTimer()
	{
		// A snapshot written before the layer timer existed carries no property at all: the
		// continued layer then keeps the game's own (restarted) timer, and the value is not
		// invented here.
		var cutNative = new FakeNativeWorldFacts();
		cutNative.SeedRunFields(1f, 1f, 30f, Recipe(0, madeBefore: true, intValue: 0));
		cutNative.SeedLayerTime(null);

		using var fixture = WorldSaveFixture.Create("run-fields-layer-absent", nativeWorldFacts: cutNative);
		Assert.True(fixture.Service.TryBeginRun(isTutorial: false));
		Assert.True(fixture.Kernel.TryStartRun(HostId, Run(layerIndex: 1), out _, out _));
		Assert.True(Cut(fixture).Captured);
		Assert.True(fixture.Repository.Repository.SetLastOpenedWorld(fixture.WorldId));

		var restoreNative = new FakeNativeWorldFacts();
		using var restarted = WorldSaveFixture.Create("run-fields-layer-absent-restart", repository: fixture.Repository, nativeWorldFacts: restoreNative);

		Assert.True(restarted.Service.TryContinue(out var outcome), outcome.Summary);

		Assert.Contains("apply-cut-run-fields", restoreNative.Calls);
		Assert.Null(restoreNative.RunFields.LayerTimeSpent);
		Assert.DoesNotContain(restoreNative.ClockWrites, write => write.StartsWith("layer-time ", StringComparison.Ordinal));
	}

	[Fact]
	public void MidRunCut_WhileTheWorldIsAlreadyOnTheNextLayer_KeepsTheBaselineMultipliers()
	{
		// The game accumulates the NEXT layer's multipliers while it clears the old
		// one (WorldGeneration.cs:1061-1062, before the kernel commits the advance), so
		// a cut taken in that window sees live values that no longer belong to the
		// layer its baseline names. Stamping the live read there would rebuild the
		// named layer with the next layer's loot/trap density.
		var native = new FakeNativeWorldFacts();
		native.SeedRunFields(9f, 9f, 10f);

		using var fixture = WorldSaveFixture.Create("run-fields-window", nativeWorldFacts: native);
		Assert.True(fixture.Service.TryBeginRun(isTutorial: false));
		Assert.True(fixture.Kernel.TryStartRun(HostId, Run(layerIndex: 1, loot: 2f, trap: 3f), out _, out _));

		Assert.True(Cut(fixture).Captured);

		var baseline = LiveRunRows(fixture).Single(row => KindOf(row) == SaveRunRow.RunKind).GetProperty("run");
		Assert.Equal(2f, baseline.GetProperty("lootRarityMultiplier").GetSingle());
		Assert.Equal(3f, baseline.GetProperty("trapRarityMultiplier").GetSingle());
	}

	[Fact]
	public void LayerEndCut_CarriesTheRunFieldsAndStampsTheBaseline()
	{
		var native = new FakeNativeWorldFacts();
		native.SeedRunFields(4f, 5f, 11.5f, Recipe(1, madeBefore: true, intValue: 0));
		native.SeedLayerTime(500f);

		using var fixture = WorldSaveFixture.Create("run-fields-layerend", nativeWorldFacts: native);
		Assert.True(fixture.Service.TryBeginRun(isTutorial: false));
		Assert.True(fixture.Kernel.TryStartRun(HostId, Run(layerIndex: 0), out _, out _));

		// The layer advance is where the host publishes the boundary capture, so the
		// multipliers the layer it names was generated with ride the new baseline.
		Assert.True(fixture.Kernel.TryAdvanceLayer(HostId, Run(layerIndex: 1, loot: 4f, trap: 5f), out _, out _));

		var rows = LiveRunRows(fixture);
		Assert.Equal(4f, rows.Single(row => KindOf(row) == SaveRunRow.RunKind).GetProperty("run").GetProperty("lootRarityMultiplier").GetSingle());
		var fields = rows.Single(row => KindOf(row) == SaveRunRow.NativeRunFieldsKind).GetProperty("nativeRunFields");
		Assert.Equal(11.5f, fields.GetProperty("savedRunTime").GetSingle());
		// The layer timer is an IN-LAYER fact and this cut names a layer that is
		// regenerated: recording the replaced layer's elapsed time would hand the new
		// layer a countdown it never spent.
		Assert.False(fields.TryGetProperty("layerTimeSpent", out _), "a layer-end cut must not carry the replaced layer's timer");

		// The layer-end contract is untouched: the run fields are properties of the
		// RUN, so they travel with this cut too, while the two in-layer files stay
		// empty (the layer it names is regenerated from the baseline).
		Assert.Empty(LiveEntries(fixture, SaveArchiveFormat.WorldBlocksFileName));
		Assert.Empty(LiveEntries(fixture, SaveArchiveFormat.WorldTransientsFileName));
		Assert.Contains("capture-run-fields", native.Calls);
		Assert.DoesNotContain("capture-native", native.Calls);
	}

	[Fact]
	public void UnreadableRunFields_RefuseTheCut()
	{
		var native = new FakeNativeWorldFacts { CaptureRunFieldsFailure = "no live world holds the run clock" };

		using var fixture = WorldSaveFixture.Create("run-fields-unreadable", nativeWorldFacts: native);
		Assert.True(fixture.Service.TryBeginRun(isTutorial: false));
		Assert.True(fixture.Kernel.TryStartRun(HostId, Run(layerIndex: 0), out _, out _));

		var report = Cut(fixture);

		// A snapshot that reads back as "clock zero, nothing unlocked" is worse than
		// no snapshot: the refusal keeps the previous one intact and says why.
		Assert.False(report.Captured);
		Assert.Contains("no live world holds the run clock", report.Summary, StringComparison.Ordinal);
		Assert.False(Directory.Exists(fixture.Repository.Workspace.LiveDirectory(fixture.WorldId)));
	}

	[Fact]
	public void Continue_HandsTheRestoredRunFieldsToTheNativeApplier()
	{
		var cutNative = new FakeNativeWorldFacts();
		cutNative.SeedRunFields(6f, 7f, 88.5f, Recipe(2, madeBefore: true, intValue: 0));

		using var fixture = WorldSaveFixture.Create("run-fields-continue", nativeWorldFacts: cutNative);
		Assert.True(fixture.Service.TryBeginRun(isTutorial: false));
		Assert.True(fixture.Kernel.TryStartRun(HostId, Run(layerIndex: 3, loot: 6f, trap: 7f), out _, out _));
		Assert.True(Cut(fixture).Captured);
		Assert.True(fixture.Repository.Repository.SetLastOpenedWorld(fixture.WorldId));

		var restoreNative = new FakeNativeWorldFacts();
		using var restarted = WorldSaveFixture.Create("run-fields-continue-restart", repository: fixture.Repository, nativeWorldFacts: restoreNative);

		Assert.True(restarted.Service.TryContinue(out var outcome), outcome.Summary);

		// The archive's clock base and unlock table are handed over, not written from
		// the Runtime (the world does not exist at the click).
		Assert.Contains("apply-cut-run-fields", restoreNative.Calls);
		Assert.Equal(88.5f, restoreNative.RunFields.SavedRunTime);
		var restored = Assert.Single(restoreNative.RunFields.Recipes);
		Assert.Equal(2, restored.Index);
		Assert.True(restored.MadeBefore);

		// And the multipliers came back through the run baseline, which is what a
		// peer that generates the layer from this snapshot would be handed.
		var run = restarted.Kernel.QueryRun();
		Assert.NotNull(run);
		Assert.Equal(6f, run!.LootRarityMultiplier);
		Assert.Equal(7f, run.TrapRarityMultiplier);
		Assert.DoesNotContain("native run fields", outcome.Summary, StringComparison.Ordinal);
	}

	[Fact]
	public void Continue_WithoutTheNativeRow_NamesTheGapInsteadOfWritingDefaults()
	{
		// A composition with no native reader records no run-field row at all — the
		// shape an archive written before this row existed has.
		using var fixture = WorldSaveFixture.Create("run-fields-absent");
		Assert.True(fixture.Service.TryBeginRun(isTutorial: false));
		Assert.True(fixture.Kernel.TryStartRun(HostId, Run(layerIndex: 1), out _, out _));
		Assert.True(Cut(fixture).Captured);
		Assert.Single(LiveRunRows(fixture));
		Assert.True(fixture.Repository.Repository.SetLastOpenedWorld(fixture.WorldId));

		var restoreNative = new FakeNativeWorldFacts();
		using var restarted = WorldSaveFixture.Create("run-fields-absent-restart", repository: fixture.Repository, nativeWorldFacts: restoreNative);

		Assert.True(restarted.Service.TryContinue(out var outcome), outcome.Summary);

		Assert.Contains("carries no native run fields", outcome.Summary, StringComparison.Ordinal);
		Assert.DoesNotContain("apply-cut-run-fields", restoreNative.Calls);
	}

	[Fact]
	public void Continue_WithoutANativeApplier_NamesTheRunFieldsItCouldNotRestore()
	{
		var cutNative = new FakeNativeWorldFacts();
		cutNative.SeedRunFields(1f, 1f, 5f, Recipe(0, madeBefore: true, intValue: 0));

		using var fixture = WorldSaveFixture.Create("run-fields-no-applier", nativeWorldFacts: cutNative);
		Assert.True(fixture.Service.TryBeginRun(isTutorial: false));
		Assert.True(fixture.Kernel.TryStartRun(HostId, Run(layerIndex: 0), out _, out _));
		Assert.True(Cut(fixture).Captured);
		Assert.True(fixture.Repository.Repository.SetLastOpenedWorld(fixture.WorldId));

		using var restarted = WorldSaveFixture.Create("run-fields-no-applier-restart", repository: fixture.Repository);

		Assert.True(restarted.Service.TryContinue(out var outcome), outcome.Summary);

		// The account must name the run fields themselves, not a zero count of the
		// layer facts the snapshot never carried.
		Assert.Contains("no native applier", outcome.Summary, StringComparison.Ordinal);
		Assert.Contains("run clock base", outcome.Summary, StringComparison.Ordinal);
		Assert.Contains("1 recipe unlock row(s)", outcome.Summary, StringComparison.Ordinal);
	}

	// ---- helpers ----

	private static SaveRecipeUnlockRow Recipe(int index, bool madeBefore, int intValue) =>
		new() { Index = index, MadeBefore = madeBefore, IntValue = intValue };

	private static RunState Run(int layerIndex, float loot = RunRarityMultipliers.Neutral, float trap = RunRarityMultipliers.Neutral) =>
		new(7UL, [1, 2, 3, 4], 0, 2, 10, false, [new RunSetting("speed", RunSettingKind.Float, FloatValue: 1.5f)], layerIndex, loot, trap);

	/// <summary>Take a cut the way the pump does: arm it, then take it at the frame-end seam.</summary>
	private static WorldCutReport Cut(WorldSaveFixture fixture)
	{
		Assert.True(fixture.Service.TryRequestCut(WorldCutReason.MenuReturn, out var refusal), refusal);
		return Assert.IsType<WorldCutReport>(fixture.Service.TryCaptureArmedCut(null, frame: 0));
	}

	private static IReadOnlyList<JsonElement> LiveRunRows(WorldSaveFixture fixture) =>
		LiveEntries(fixture, SaveArchiveFormat.RunFileName);

	private static IReadOnlyList<JsonElement> LiveEntries(WorldSaveFixture fixture, string fileName)
	{
		var path = Path.Combine(fixture.Repository.Workspace.LiveDirectory(fixture.WorldId), fileName);
		using var document = JsonDocument.Parse(File.ReadAllBytes(path));
		return [.. document.RootElement.EnumerateArray().Select(element => element.Clone())];
	}

	private static string? KindOf(JsonElement row) => row.GetProperty("kind").GetString();
}
