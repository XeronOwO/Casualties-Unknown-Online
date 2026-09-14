using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Session.Persistence;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// The consistent-cut seam: a trigger ARMS a cut, the pump takes it at the
/// frame-end point, and the transient policy decides whether the cut can be taken
/// yet. The suite pins the three outcomes a trigger can get (captured, deferred,
/// refused), the deadline that stops a stuck in-flight state from starving the
/// request, and the rule that a state the cut does not carry is NAMED (§6).
/// </summary>
public class WorldSaveCutSeamTests
{
	private const ulong HostId = 1001UL;

	[Fact]
	public void ArmedCut_IsTakenAtTheSeamAndClearsTheRequest()
	{
		using var fixture = Started("seam-armed");

		Assert.True(fixture.Service.TryRequestCut(WorldCutReason.Command, out _));
		Assert.True(fixture.Service.HasArmedCut);

		var report = Assert.IsType<WorldCutReport>(fixture.Service.TryCaptureArmedCut(null, frame: 12));

		Assert.True(report.Captured);
		Assert.Equal(WorldCutReason.Command, report.Reason);
		Assert.Equal(fixture.WorldId, report.WorldId);
		Assert.Contains(WorldSaveService.FrameEndCutPhase, Manifest(fixture, "cutPhase"), StringComparison.Ordinal);
		Assert.Equal("command", Manifest(fixture, "saveReason"));
		Assert.Equal("mid-run", Manifest(fixture, "kind"));
		Assert.False(fixture.Service.HasArmedCut);
		Assert.Single(CutFiles(fixture));
	}

	[Fact]
	public void ArmedCut_ReportsTheTriggerToTheSurface()
	{
		using var fixture = Started("seam-report");
		var reports = new List<WorldCutReport>();
		fixture.Service.CutReported += reports.Add;

		Assert.True(fixture.Service.TryRequestCut(WorldCutReason.Command, out _));
		fixture.Service.TryCaptureArmedCut(null, frame: 0);

		var report = Assert.Single(reports);
		Assert.True(report.PlayerInitiated);
		Assert.True(report.Captured);
		Assert.Contains("cut written", report.Describe(), StringComparison.Ordinal);
	}

	[Fact]
	public void NoArmedCut_TakesNothing()
	{
		using var fixture = Started("seam-none");

		Assert.Null(fixture.Service.TryCaptureArmedCut(null, frame: 0));
		Assert.Empty(CutFiles(fixture));
	}

	[Fact]
	public void LayerEndTrigger_CannotBeArmedAtTheSeam()
	{
		// A layer-end cut's phase and baseline belong to the kernel's own
		// layer-advance commit; arming one here would write a payload whose kind and
		// phase disagree.
		using var fixture = Started("seam-layer-end");

		Assert.False(fixture.Service.TryRequestCut(WorldCutReason.LayerAdvance, out var refusal));
		Assert.Contains("layer-advance commit", refusal!, StringComparison.Ordinal);
		Assert.False(fixture.Service.HasArmedCut);
	}

	[Fact]
	public void BreakWindow_DefersTheCutAndKeepsItArmed()
	{
		using var fixture = Started("seam-defer");
		var reports = new List<WorldCutReport>();
		fixture.Service.CutReported += reports.Add;
		Assert.True(fixture.Service.TryRequestCut(WorldCutReason.Command, out _));
		var opening = Live(WorldTransientPolicy.BlockBreakPendingKey, 1);

		var first = Assert.IsType<WorldCutReport>(fixture.Service.TryCaptureArmedCut(null, frame: 10, opening));
		var second = Assert.IsType<WorldCutReport>(fixture.Service.TryCaptureArmedCut(null, frame: 11, opening));

		Assert.Equal(WorldCutResult.Deferred, first.Result);
		Assert.Equal(WorldCutResult.Deferred, second.Result);
		Assert.True(fixture.Service.HasArmedCut);
		Assert.Empty(CutFiles(fixture));
		Assert.Empty(reports); // a deferral is not a result: nothing is answered yet
	}

	[Fact]
	public void BreakWindow_Resolved_TakesTheCutOnTheNextFrame()
	{
		using var fixture = Started("seam-resolved", native: new FakeNativeWorldFacts());
		Assert.True(fixture.Service.TryRequestCut(WorldCutReason.Command, out _));
		Assert.Equal(WorldCutResult.Deferred, fixture.Service.TryCaptureArmedCut(null, frame: 4, Live(WorldTransientPolicy.TrapDropHoldKey, 1))!.Result);

		// The window closed (the flush registered the drops): the same frame's seam
		// takes the cut, and the report names no COUNTED loss — only the classes no
		// mid-run cut carries (the game-owned ones, which no counter can describe).
		var report = Assert.IsType<WorldCutReport>(fixture.Service.TryCaptureArmedCut(null, frame: 5, Live(WorldTransientPolicy.TrapDropHoldKey, 0)));

		Assert.True(report.Captured);
		Assert.StartsWith("no mid-run cut carries", Assert.Single(report.DroppedStates), StringComparison.Ordinal);
	}

	[Fact]
	public void BreakWindow_OutlastingTheDeadline_IsNamedInTheReport()
	{
		using var fixture = Started("seam-stuck");
		Assert.True(fixture.Service.TryRequestCut(WorldCutReason.Command, out _));
		var opening = Live(WorldTransientPolicy.DropFlushKey, 1);

		Assert.Equal(WorldCutResult.Deferred, fixture.Service.TryCaptureArmedCut(null, frame: 100, opening)!.Result);
		var report = Assert.IsType<WorldCutReport>(fixture.Service.TryCaptureArmedCut(null, frame: 100 + WorldSaveService.MaxCutDeferralFrames, opening));

		// A stuck pending record must not starve the request: the cut goes on and
		// NAMES what it could not take.
		Assert.True(report.Captured);
		Assert.True(
			report.DroppedStates.Any(row => row.IndexOf("drop report(s)", StringComparison.Ordinal) >= 0),
			string.Join(", ", report.DroppedStates));
	}

	[Fact]
	public void ReArmingTheSameTrigger_KeepsItsDeferralWindow()
	{
		// The menu-return seam re-arms its cut every frame it retries: a re-arm of
		// the SAME trigger must not restart the wait, or a stuck in-flight state
		// would defer for ever and the host would never leave the world.
		using var fixture = Started("seam-rearm");
		var opening = Live(WorldTransientPolicy.BlockBreakPendingKey, 1);
		Assert.True(fixture.Service.TryRequestCut(WorldCutReason.MenuReturn, out _));
		Assert.Equal(WorldCutResult.Deferred, fixture.Service.TryCaptureArmedCut(null, frame: 200, opening)!.Result);

		Assert.True(fixture.Service.TryRequestCut(WorldCutReason.MenuReturn, out _));
		var report = Assert.IsType<WorldCutReport>(fixture.Service.TryCaptureArmedCut(null, frame: 200 + WorldSaveService.MaxCutDeferralFrames, opening));

		Assert.True(report.Captured);
		Assert.NotEmpty(report.DroppedStates);
	}

	[Fact]
	public void DroppedState_IsNamedWhileTheCutStillSucceeds()
	{
		using var fixture = Started("seam-dropped");
		Assert.True(fixture.Service.TryRequestCut(WorldCutReason.Command, out _));

		var report = Assert.IsType<WorldCutReport>(fixture.Service.TryCaptureArmedCut(
			null, frame: 0, Live(WorldTransientPolicy.PickupQueueKey, 2)));

		Assert.True(report.Captured);
		Assert.Contains("2 pickup claim(s) waiting for their spawn report", report.DroppedStates);
		Assert.Contains("NOT carried", report.Describe(), StringComparison.Ordinal);
	}

	[Fact]
	public void StandingRows_AreNamedEvenThoughNoCounterCanSeeThem()
	{
		// The game-owned classes (the craft coroutine, item velocity, the run clock,
		// the physics timers) have no CUO counter. Claiming "nothing was in flight"
		// would be the silent loss the policy exists to prevent, so every cut names
		// the classes it never carries.
		using var fixture = Started("seam-standing", native: new FakeNativeWorldFacts());
		Assert.True(fixture.Service.TryRequestCut(WorldCutReason.Command, out _));

		var report = Assert.IsType<WorldCutReport>(fixture.Service.TryCaptureArmedCut(null, frame: 0));

		var standing = Assert.Single(report.DroppedStates);
		Assert.Contains("craft batch(es) in progress", standing, StringComparison.Ordinal);
		Assert.Contains("item physics transient(s) (velocity/rotation)", standing, StringComparison.Ordinal);
		Assert.Contains("world-clock state", standing, StringComparison.Ordinal);
		Assert.Contains("earthquake timer(s)", standing, StringComparison.Ordinal);
	}

	[Fact]
	public void WithoutANativeReader_TheCutNamesWhatItCannotCarry()
	{
		// A composition with no INativeWorldFacts can still cut (the Runtime facts
		// ride), but the decided native values and the game's own partial damage do
		// not — that must reach the report, not only the log.
		using var fixture = Started("seam-no-native");
		Assert.True(fixture.Service.TryRequestCut(WorldCutReason.Command, out _));

		var report = Assert.IsType<WorldCutReport>(fixture.Service.TryCaptureArmedCut(null, frame: 0));

		Assert.True(report.Captured);
		Assert.Contains(report.DroppedStates, row => row.IndexOf("no native world-fact reader", StringComparison.Ordinal) >= 0);
	}

	[Fact]
	public void Observation_MergesBothHalvesOfTheSameClass()
	{
		// Both observers can see the same class (a pending pickup is a Runtime queue
		// entry, and an adapter-side window could report the same key). The report
		// must count BOTH, not the last one read.
		var probe = new StubProbe(WorldTransientPolicy.PickupQueueKey, 1);
		using var fixture = Started("seam-merge", transients: probe);
		Assert.True(fixture.Service.TryRequestCut(WorldCutReason.Command, out _));

		var report = Assert.IsType<WorldCutReport>(fixture.Service.TryCaptureArmedCut(
			null, frame: 0, Live(WorldTransientPolicy.PickupQueueKey, 2)));

		Assert.Contains("3 pickup claim(s) waiting for their spawn report", report.DroppedStates);
	}

	[Fact]
	public void UndeclaredTransientClass_RefusesTheCutAndWritesNothing()
	{
		using var fixture = Started("seam-undeclared");
		Assert.True(fixture.Service.TryRequestCut(WorldCutReason.Command, out _));

		var report = Assert.IsType<WorldCutReport>(fixture.Service.TryCaptureArmedCut(
			null, frame: 0, Live("ghost-class", 1)));

		// An owner bug must not become a snapshot whose in-flight state is
		// unaccounted for.
		Assert.Equal(WorldCutResult.Refused, report.Result);
		Assert.Contains("ghost-class", report.Summary, StringComparison.Ordinal);
		Assert.False(fixture.Service.HasArmedCut);
		Assert.Empty(CutFiles(fixture));
	}

	[Fact]
	public void UnreadableNativeTable_RefusesTheCutAndWritesNothing()
	{
		// The game's own partial-damage list is the ONLY table that holds partial
		// damage. A reader that met no live world must not be read as "no damage":
		// the cut is refused rather than storing a clean-looking world.
		var native = new FakeNativeWorldFacts { CaptureFailure = "no live world is present" };
		using var fixture = Started("seam-unreadable-native", native);
		Assert.True(fixture.Service.TryRequestCut(WorldCutReason.Command, out _));

		var report = Assert.IsType<WorldCutReport>(fixture.Service.TryCaptureArmedCut(null, frame: 0));

		Assert.Equal(WorldCutResult.Refused, report.Result);
		Assert.Contains("no live world is present", report.Summary, StringComparison.Ordinal);
		Assert.Empty(CutFiles(fixture));
	}

	[Fact]
	public void MenuReturn_SupersedesAQueuedCommandCut_OneSnapshotOneReason()
	{
		using var fixture = Started("seam-supersede");
		Assert.True(fixture.Service.TryRequestCut(WorldCutReason.Command, out _));

		Assert.True(fixture.Service.TryRequestCut(WorldCutReason.MenuReturn, out _));
		var report = Assert.IsType<WorldCutReport>(fixture.Service.TryCaptureArmedCut(null, frame: 0));

		Assert.Equal(WorldCutReason.MenuReturn, report.Reason);
		Assert.Single(CutFiles(fixture));
		Assert.False(fixture.Service.HasArmedCut);
	}

	[Fact]
	public void NewRun_DropsARequestArmedForThePreviousWorld()
	{
		using var fixture = Started("seam-new-run");
		Assert.True(fixture.Service.TryRequestCut(WorldCutReason.Command, out _));

		Assert.True(fixture.Service.TryBeginRun(isTutorial: false));

		// The armed request belongs to the world it was armed in; the new run must
		// never inherit it.
		Assert.False(fixture.Service.HasArmedCut);
		Assert.Null(fixture.Service.TryCaptureArmedCut(null, frame: 0));
	}

	[Fact]
	public void RuntimeHalfOfTheObservation_IsMergedIntoTheCutReport()
	{
		// The adapter reports the game-side windows; the Runtime probe reports the
		// CUO-owned ones (the operation sessions, the pickup queue, the deferred
		// creation reports). A cut must see BOTH halves, or a session in progress
		// would be left behind without a word.
		var probe = new StubProbe(WorldTransientPolicy.MedicalSessionKey, 1);
		using var fixture = Started("seam-runtime-half", transients: probe);
		Assert.True(fixture.Service.TryRequestCut(WorldCutReason.Command, out _));

		var report = Assert.IsType<WorldCutReport>(fixture.Service.TryCaptureArmedCut(
			null, frame: 0, Live(WorldTransientPolicy.PickupQueueKey, 1)));

		Assert.True(report.Captured);
		Assert.Contains("1 medical operation(s) in progress", report.DroppedStates);
		Assert.Contains("1 pickup claim(s) waiting for their spawn report", report.DroppedStates);
	}

	[Fact]
	public void RuntimeHalf_ResolveRow_DefersTheCutToo()
	{
		var probe = new StubProbe(WorldTransientPolicy.DropFlushKey, 1);
		using var fixture = Started("seam-runtime-defer", transients: probe);
		Assert.True(fixture.Service.TryRequestCut(WorldCutReason.Command, out _));

		var report = Assert.IsType<WorldCutReport>(fixture.Service.TryCaptureArmedCut(null, frame: 0));

		Assert.Equal(WorldCutResult.Deferred, report.Result);
		Assert.True(fixture.Service.HasArmedCut);
		Assert.Empty(CutFiles(fixture));
	}

	// ---- fixture ----

	[Fact]
	public void Cut_TwoPresentPlayersSharingADisplayName_CarriesNeitherAndNamesThem()
	{
		// The archive holds one character per transport-scoped key, so two present IP-direct
		// players sharing a display name map to ONE key. Writing both files made the writer
		// refuse EVERY cut (duplicate payload path) with a message that named no cause, and
		// writing one of them would leave a file a later restore could claim for the wrong
		// player — so the cut carries neither and says who it left behind (decision 177).
		using var fixture = WorldSaveFixture.Create("seam-shared-key", ipDirect: true, displayName: "Bob");
		Assert.True(fixture.Service.TryBeginRun(isTutorial: false));
		Assert.True(fixture.Kernel.TryStartRun(HostId, WorldSaveCaptureTests.Run(layerIndex: 0), out _, out _));
		fixture.Session.AddMember(2002UL, "Bob");
		fixture.Characters.SaveHostCharacterData(WorldSaveCaptureTests.Character(100, "bag"));
		fixture.Characters.SaveCharacterData(2002UL, WorldSaveCaptureTests.Character(200, "rope"));

		Assert.True(fixture.Service.TryRequestCut(WorldCutReason.Command, out var refusal), refusal);
		var report = Assert.IsType<WorldCutReport>(fixture.Service.TryCaptureArmedCut(null, frame: 0));

		Assert.True(report.Captured, report.Describe());
		Assert.Single(CutFiles(fixture)); // the cut really wrote — it is no longer refused
		Assert.Contains(report.DroppedStates, line => line.Contains("name-bob", StringComparison.Ordinal));
		Assert.Contains(report.DroppedStates, line => line.Contains("1001, 2002", StringComparison.Ordinal));
		Assert.Contains("NOT carried", report.Describe(), StringComparison.Ordinal);

		// And the archive it wrote carries no character for that key, so the restore hands
		// nobody a character rather than picking one of the two.
		using var restarted = fixture.Restart("seam-shared-key-restart", ipDirect: true, displayName: "Bob");
		Assert.True(restarted.Service.TryContinue(out var outcome), outcome.Summary);
		Assert.Empty(restarted.Service.PendingCharacters);
		Assert.Null(restarted.Characters.GetHostCharacterData());
	}

	private sealed class StubProbe(string key, int pending) : IWorldCutTransientProbe
	{
		public IReadOnlyList<WorldTransientCount> Capture() => [new(key, pending)];
	}

	private static WorldSaveFixture Started(string label, FakeNativeWorldFacts? native = null, IWorldCutTransientProbe? transients = null)
	{
		var fixture = WorldSaveFixture.Create(label, nativeWorldFacts: native, transients: transients);
		Assert.True(fixture.Service.TryBeginRun(isTutorial: false));
		Assert.True(fixture.Kernel.TryStartRun(HostId, WorldSaveCaptureTests.Run(layerIndex: 0), out _, out _));
		return fixture;
	}

	private static IReadOnlyList<WorldTransientCount> Live(string key, int pending) => [new(key, pending)];

	private static IReadOnlyList<string> CutFiles(WorldSaveFixture fixture)
	{
		var directory = fixture.Repository.Workspace.BackupsDirectory(fixture.WorldId);
		return Directory.Exists(directory) ? Directory.GetFiles(directory, "*.cuoz") : [];
	}

	private static string Manifest(WorldSaveFixture fixture, string property) =>
		WorldSaveCaptureTests.LiveManifest(fixture).GetProperty(property).GetString()!;
}
