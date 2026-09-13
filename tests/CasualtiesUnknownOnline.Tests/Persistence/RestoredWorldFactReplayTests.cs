using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Persistence;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// The restored-world replay's contract: WHICH rows reach the live world, in
/// which order, and what happens to a cut the world cannot take yet. The writes
/// themselves belong to the Game Adapter (behind
/// <see cref="IRestoredWorldFactSink"/>), so this suite runs without a game.
///
/// The handover is a READ-THEN-COMMIT: a generation that cannot take every value
/// must keep BOTH halves pending, or a retry would write an empty cut over tables
/// the world never received.
/// </summary>
public sealed class RestoredWorldFactReplayTests
{
	/// <summary>
	/// The ONE restore attempt these suites drive: the kernel restore sequence the fake
	/// world-fact tables are applied with, the account is opened for, and the entity arm
	/// is stamped with. It is the identity a contribution is attributed by, so a suite
	/// that models a second restore passes its own value.
	/// </summary>
	private const ulong Attempt = 1;

	[Fact]
	public void ApplyIfPending_WithNothingPending_LeavesTheLiveWorldAlone()
	{
		var (replay, _, _, sink) = Build();

		replay.ApplyIfPending();

		Assert.Empty(sink.Calls);
	}

	[Fact]
	public void HasPending_WithOnlyTheItemReconcileOwed_KeepsTheGateTrue()
	{
		// A restore whose block diff, native handover and world-entity facts have all
		// landed can still owe the ITEM half: the generation has not reconciled its
		// objects against the restored world items yet. The world-entry seam asks this
		// gate BEFORE it decides whether to run the layer-boundary reset, and that
		// reset drops every world-rooted row — taking it here would erase the very set
		// the reconcile is about to materialize, and the loss would be silent.
		var items = new FakeRestoredWorldItemSource { Armed = true };
		var replay = new RestoredWorldFactReplay(
			new FakeWorldFactSource(),
			new FakeNativeWorldFacts(),
			new FakeRestoredWorldFactSink(),
			new RecordingLogger<RestoredWorldFactReplay>(),
			audit: null,
			worldEntities: null,
			restoredWorldItems: items);

		Assert.True(replay.HasPending);
	}

	[Fact]
	public void HasPending_WithEverySourceWiredAndOnlyTheItemReconcileOwed_KeepsTheGateTrue()
	{
		// The production shape: all four sources are wired (the composition root passes
		// the item control it already holds), and only the item half is still owed. A
		// suite that armed this one through a null world-entity source would not notice
		// a gate that dropped the arm whenever another source was present.
		var items = new FakeRestoredWorldItemSource { Armed = true };
		var replay = new RestoredWorldFactReplay(
			new FakeWorldFactSource(),
			new FakeNativeWorldFacts(),
			new FakeRestoredWorldFactSink(),
			new RecordingLogger<RestoredWorldFactReplay>(),
			audit: new WorldRestoreAudit(),
			worldEntities: new FakeRestoredWorldEntitySource(),
			restoredWorldItems: items);

		Assert.True(replay.HasPending);
	}

	[Fact]
	public void HasPending_WithEveryHalfLanded_IsFalse()
	{
		var replay = new RestoredWorldFactReplay(
			new FakeWorldFactSource(),
			new FakeNativeWorldFacts(),
			new FakeRestoredWorldFactSink(),
			new RecordingLogger<RestoredWorldFactReplay>(),
			audit: null,
			worldEntities: null,
			restoredWorldItems: new FakeRestoredWorldItemSource());

		Assert.False(replay.HasPending);
	}

	[Fact]
	public void ApplyIfPending_WhenTheWorldIsNotReady_KeepsTheCutPending()
	{
		var (replay, facts, native, sink) = Build();
		facts.ApplyFacts([new BlockStateEntryMsg { X = 1, Y = 2, Block = 0 }], null, Attempt);
		sink.WorldReady = false;
		Assert.True(replay.HasPending);

		replay.ApplyIfPending();

		// The entry seam runs once per generation: a half-generated world must not
		// consume the values, or they would be dropped silently.
		Assert.True(facts.HasPendingLiveReplay);
		Assert.True(replay.HasPending);
		Assert.DoesNotContain("read-pending", native.Calls);
		Assert.Empty(sink.Calls);
	}

	[Fact]
	public void ApplyIfPending_WritesTheCutInTheLoadBearingOrder()
	{
		var (replay, facts, native, sink) = Build();
		facts.ApplyFacts(
			[new BlockStateEntryMsg { X = 1, Y = 2, Block = 0 }],
			new RadiationLineStateMsg { Active = true, TimeGone = 1f }, Attempt);
		native.SeedKeypad(5f, 6f, "1234");
		native.SeedGeyser(7f, 8f, 3);
		native.SeedBlockDamage(9, 10, 4f);
		native.ApplyKeypadCodes(native.Keypads);
		native.ApplyGeysers(native.Geysers);
		native.ApplyBlockDamages(native.Damages);
		native.ApplyRecipeUnlocks([new SaveRecipeUnlockRow { Index = 2, MadeBefore = true, IntValue = 0 }]);

		replay.ApplyIfPending();

		Assert.Equal(
			["write-block-states", "replace-game-damages", "apply-keypads", "apply-geysers", "apply-recipes", "apply-radiation"],
			sink.Calls);
		Assert.Equal(1, sink.WrittenBlockStates.Count);
		Assert.Equal(1, sink.AppliedKeypads);
		Assert.Equal(1, sink.AppliedGeysers);
		Assert.Single(sink.AppliedRecipes);
		Assert.False(facts.HasPendingLiveReplay);
		Assert.False(replay.HasPending);
		Assert.Contains("commit-pending", native.Calls);
	}

	/// <summary>
	/// The game's own damage rows reach the game's own list, and nothing else does:
	/// the Runtime half of the cut has no partial-damage rows to contribute (CUO
	/// keeps no such table), so every row the live list receives came from the
	/// game's table the cut captured.
	/// </summary>
	[Fact]
	public void ApplyIfPending_WritesTheGameBlockDamageRowsIntoTheGameList()
	{
		var (replay, facts, native, sink) = Build();
		facts.ApplyFacts([new BlockStateEntryMsg { X = 1, Y = 2, Block = 0 }], null, Attempt);
		native.SeedBlockDamage(1000, 1000, 7f);
		native.ApplyBlockDamages(native.Damages);

		replay.ApplyIfPending();

		var cell = Assert.Single(sink.GameDamageTable);
		Assert.Equal(1000, cell.X);
		Assert.Equal(1000, cell.Y);
		Assert.Equal(7f, cell.Damage);
	}

	[Fact]
	public void ApplyIfPending_WhenTheLiveWorldRefusesRows_ReportsTheLossAndReleasesBothHandovers()
	{
		var facts = new FakeWorldFactSource();
		var native = new FakeNativeWorldFacts();
		var sink = new FakeRestoredWorldFactSink { Capacity = 0 };
		var log = new RecordingLogger<RestoredWorldFactReplay>();
		var replay = new RestoredWorldFactReplay(facts, native, sink, log);
		facts.ApplyFacts([new BlockStateEntryMsg { X = 1, Y = 2, Block = 0 }], null, Attempt);
		native.SeedBlockDamage(1, 1, 1f);
		native.SeedBlockDamage(2, 2, 1f);
		native.ApplyBlockDamages(native.Damages);

		replay.ApplyIfPending();

		// A restored crack the world did not take is lost state: it must reach the
		// log at error level, and BOTH handovers stay armed — the Runtime marker and
		// the adapter's pending rows — or the retry below would write an empty cut.
		Assert.True(log.HasError("the restored state is INCOMPLETE"));
		Assert.False(facts.HasPendingLiveReplay);
		Assert.False(native.HasPendingRestore);
		Assert.Contains("read-pending", native.Calls);
		Assert.Contains("cancel-pending", native.Calls);
		Assert.DoesNotContain("commit-pending", native.Calls);
		Assert.Empty(sink.GameDamageTable);
	}

	[Fact]
	public void ApplyIfPending_WhenTheWorldVanishesBeforeTheWrite_ReportsTheLoss()
	{
		var facts = new FakeWorldFactSource();
		var native = new FakeNativeWorldFacts();
		var sink = new FakeRestoredWorldFactSink { RefuseBlockWrites = true };
		var log = new RecordingLogger<RestoredWorldFactReplay>();
		var replay = new RestoredWorldFactReplay(facts, native, sink, log);
		facts.ApplyFacts([new BlockStateEntryMsg { X = 1, Y = 2, Block = 0 }], null, Attempt);

		replay.ApplyIfPending();

		// The readiness check passed and the world was gone by the time the rows
		// landed: the sink refuses every row, and the restore must stay armed so the
		// next generation retries instead of letting the layer reset wipe the tables.
		Assert.False(facts.HasPendingLiveReplay);
		Assert.True(log.HasError("the restored state is INCOMPLETE"));
	}

	[Fact]
	public void ApplyIfPending_WhenAKeypadHasNoLiveOpenable_ReportsTheLoss()
	{
		var facts = new FakeWorldFactSource();
		var native = new FakeNativeWorldFacts();
		var sink = new FakeRestoredWorldFactSink { RefuseKeypads = 1 };
		var log = new RecordingLogger<RestoredWorldFactReplay>();
		var replay = new RestoredWorldFactReplay(facts, native, sink, log);
		facts.ApplyFacts([new BlockStateEntryMsg { X = 1, Y = 2, Block = 0 }], null, Attempt);
		native.SeedKeypad(5f, 6f, "1234");
		native.ApplyKeypadCodes(native.Keypads);

		replay.ApplyIfPending();

		// "Zero Openables matched" can never mean "restored": the world-entry
		// broadcast that follows would hand every peer a freshly rolled code.
		Assert.True(log.HasError("the restored state is INCOMPLETE"));
		Assert.False(facts.HasPendingLiveReplay);
		Assert.False(native.HasPendingRestore);
	}

	[Fact]
	public void ApplyIfPending_RecipeRowsTheWorldCannotTake_ReachTheRestoreAccount()
	{
		// The recipe unlock table is applied at THIS seam (the world's recipe table is
		// only complete here — the game rebuilds it in Awake and CUO's mod-content
		// provider appends the custom recipes on a later Update frame), so a row whose
		// recipe is gone must reach the caller that started the restore, not only the
		// log: a silently re-locked recipe is a value the player can see.
		var facts = new FakeWorldFactSource();
		var native = new FakeNativeWorldFacts();
		var sink = new FakeRestoredWorldFactSink { RefuseRecipes = 1 };
		var audit = new WorldRestoreAudit();
		var reports = new List<WorldRestoreLiveWriteReport>();
		audit.Reported += reports.Add;
		facts.ApplyFacts([], null, Attempt); // the click applies the cut's fact set, then opens the account for that attempt
		audit.BeginRestore("w-recipes", Attempt);
		var replay = new RestoredWorldFactReplay(facts, native, sink, new RecordingLogger<RestoredWorldFactReplay>(), audit);
		native.ApplyRecipeUnlocks(
		[
			new SaveRecipeUnlockRow { Index = 0, MadeBefore = true, IntValue = 0 },
			new SaveRecipeUnlockRow { Index = 9, MadeBefore = true, IntValue = 0 },
		]);

		replay.ApplyIfPending();

		var report = Assert.Single(reports);
		Assert.False(report.Complete);
		Assert.Contains("1 recipe unlock row(s)", string.Join(", ", report.Refused), StringComparison.Ordinal);
		Assert.Single(sink.AppliedRecipes);

		// The release rule is unchanged: a row the world did not take ends the
		// handover instead of staying armed for the next generation.
		Assert.False(native.HasPendingRestore);
	}

	[Fact]
	public void ApplyIfPending_ReportsTheLiveWriteToTheRestoreAudit()
	{
		// The Continue click returned long before this seam ran, so the audit is the
		// only way the caller that started the restore learns what the live world
		// took. Both outcomes are reported — a complete restore and a partial one —
		// because "the click applied the kernel" is not the whole story.
		var facts = new FakeWorldFactSource();
		var native = new FakeNativeWorldFacts();
		var sink = new FakeRestoredWorldFactSink { RefuseBlockWrites = true };
		var audit = new WorldRestoreAudit();
		var reports = new List<WorldRestoreLiveWriteReport>();
		audit.Reported += reports.Add;
		audit.BeginRestore("w-audit", Attempt);
		var replay = new RestoredWorldFactReplay(facts, native, sink, new RecordingLogger<RestoredWorldFactReplay>(), audit);
		facts.ApplyFacts([new BlockStateEntryMsg { X = 1, Y = 2, Block = 0 }], null, Attempt);

		replay.ApplyIfPending();

		var report = Assert.Single(reports);
		Assert.Equal("w-audit", report.WorldId);
		Assert.False(report.Complete);
		Assert.True(
			report.Refused.Any(row => row.IndexOf("block-state", StringComparison.Ordinal) >= 0),
			string.Join(", ", report.Refused));
		Assert.False(audit.AwaitingLiveWrite);
	}

	[Fact]
	public void ApplyIfPending_CompleteRestore_ReportsAFullLiveWorld()
	{
		var facts = new FakeWorldFactSource();
		var native = new FakeNativeWorldFacts();
		var sink = new FakeRestoredWorldFactSink();
		var audit = new WorldRestoreAudit();
		var reports = new List<WorldRestoreLiveWriteReport>();
		audit.Reported += reports.Add;
		audit.BeginRestore("w-complete", Attempt);
		var replay = new RestoredWorldFactReplay(facts, native, sink, new RecordingLogger<RestoredWorldFactReplay>(), audit);
		facts.ApplyFacts([new BlockStateEntryMsg { X = 1, Y = 2, Block = 0 }], null, Attempt);

		replay.ApplyIfPending();

		var report = Assert.Single(reports);
		Assert.True(report.Complete);
		Assert.Empty(report.Refused);
		Assert.Contains("took every restored fact", report.Summary, StringComparison.Ordinal);
	}

	[Fact]
	public void ApplyIfPending_WhenTheWriteThrows_ReportsIncompleteAndReleasesBothHandovers()
	{
		// An engine call throwing mid-write is the one path the sink's own counts
		// cannot describe: nothing after the throw ran, so the replay reports the
		// loss and releases BOTH handovers — a left-armed handover would replay this
		// layer's rows into the next generation.
		var facts = new FakeWorldFactSource();
		var native = new FakeNativeWorldFacts();
		var sink = new FakeRestoredWorldFactSink { ThrowOnBlockWrite = true };
		var audit = new WorldRestoreAudit();
		var reports = new List<WorldRestoreLiveWriteReport>();
		audit.Reported += reports.Add;
		audit.BeginRestore("w-threw", Attempt);
		var replay = new RestoredWorldFactReplay(facts, native, sink, new RecordingLogger<RestoredWorldFactReplay>(), audit);
		facts.ApplyFacts([new BlockStateEntryMsg { X = 1, Y = 2, Block = 0 }], null, Attempt);

		replay.ApplyIfPending();

		var report = Assert.Single(reports);
		Assert.False(report.Complete);
		Assert.Contains("threw", Assert.Single(report.Refused), StringComparison.Ordinal);
		Assert.False(audit.AwaitingLiveWrite);
		Assert.False(facts.HasPendingLiveReplay);
		Assert.False(replay.HasPending);
	}

	[Fact]
	public void ApplyIfPending_WhenAWorldEntityRowThrows_DoesNotBlameTheHalvesThatLanded()
	{
		// A throwing ROW lives in the world-entity half, so it is that half's failure and
		// nothing else's: the block diff, the partial damage and the decided values were
		// written AND counted before the throw, and a report that names them as not taken
		// is a false alarm — while releasing their handover discards the record that they
		// did land. The half that threw is named, reported incomplete and released, exactly
		// like a REFUSED entity row (the row the regenerated layer has no entity for).
		var facts = new FakeWorldFactSource();
		var native = new FakeNativeWorldFacts();
		var sink = new FakeRestoredWorldFactSink { ThrowOnWorldEntityWrite = true };
		var entities = new FakeRestoredWorldEntitySource { Armed = true, Sequence = Attempt, Facts = EntityFacts() };
		var audit = new WorldRestoreAudit();
		var reports = new List<WorldRestoreLiveWriteReport>();
		audit.Reported += reports.Add;
		audit.BeginRestore("w-entity-threw", Attempt, expectedContributions: 2);
		var replay = new RestoredWorldFactReplay(
			facts, native, sink, new RecordingLogger<RestoredWorldFactReplay>(), audit, entities);
		facts.ApplyFacts([new BlockStateEntryMsg { X = 1, Y = 2, Block = 0 }], null, Attempt);
		native.SeedKeypad(5f, 6f, "1234");
		native.ApplyKeypadCodes(native.Keypads); // arm this half's handover: its COMMIT is what the throw must not take away

		replay.ApplyIfPending();

		var report = Assert.Single(reports);
		Assert.False(report.Complete);
		Assert.Contains("threw", string.Join("; ", report.Refused), StringComparison.Ordinal);
		Assert.DoesNotContain(
			report.Refused,
			entry => entry.IndexOf("the live-world write threw", StringComparison.Ordinal) >= 0);

		// The half that landed says so and stays committed: its rows ARE in the live world.
		Assert.Contains("took every restored fact", report.Summary, StringComparison.Ordinal);
		Assert.Contains("commit-pending", native.Calls);
		Assert.DoesNotContain("cancel-pending", native.Calls);
		Assert.False(facts.HasPendingLiveReplay);
		Assert.False(native.HasPendingRestore);

		// The half that threw is the one released — no leak into the next generation's layer.
		Assert.Equal(1, entities.Reads);
		Assert.Single(entities.Cancels);
		Assert.Equal(0, entities.Commits);
		Assert.False(entities.Armed);
		Assert.False(replay.HasPending);
	}

	[Fact]
	public void ApplyIfPending_NothingToWrite_CompletesAnAwaitingAudit()
	{
		// A layer-end cut carries no live-world fact, so its restore reaches the
		// world-entry seam with nothing to write. The audit must be told the restore
		// is complete instead of staying armed for a write that will never come — and
		// the report belongs to the attempt that applied the (empty) tables, which is
		// the order production runs: the click applies the cut's fact set, THEN opens
		// the account for it.
		var facts = new FakeWorldFactSource();
		var native = new FakeNativeWorldFacts();
		var sink = new FakeRestoredWorldFactSink();
		var audit = new WorldRestoreAudit();
		var reports = new List<WorldRestoreLiveWriteReport>();
		audit.Reported += reports.Add;
		facts.ApplyFacts([], null, Attempt);
		audit.BeginRestore("w-empty", Attempt);
		var replay = new RestoredWorldFactReplay(facts, native, sink, new RecordingLogger<RestoredWorldFactReplay>(), audit);

		replay.ApplyIfPending();

		var report = Assert.Single(reports);
		Assert.True(report.Complete);
		Assert.Equal("w-empty", report.WorldId);
		Assert.False(audit.AwaitingLiveWrite);
		Assert.Empty(sink.Calls);
	}

	[Fact]
	public void ApplyIfPending_WithOnlyTheWorldEntitiesPending_WritesAndCommitsThatHalf()
	{
		// A host restore whose world-fact tables came back empty (nothing mined, no
		// radiation line) still owes this write: the restored kernel DOES hold per-entity
		// facts, and reading "the Runtime tables are empty" as "the cut carried nothing"
		// would leave the host's regenerated world without them.
		var facts = new FakeWorldFactSource();
		var native = new FakeNativeWorldFacts();
		var sink = new FakeRestoredWorldFactSink();
		var entities = new FakeRestoredWorldEntitySource { Armed = true, Sequence = Attempt, Facts = EntityFacts() };
		var replay = new RestoredWorldFactReplay(
			facts, native, sink, new RecordingLogger<RestoredWorldFactReplay>(), worldEntities: entities);

		Assert.True(replay.HasPending);
		replay.ApplyIfPending();

		var written = Assert.Single(sink.WrittenWorldEntities);
		Assert.Equal(3, written.Count);
		Assert.Equal(1, entities.Reads);
		Assert.Equal(1, entities.Commits);
		Assert.False(entities.Armed);
		Assert.Empty(entities.Cancels);
		Assert.False(replay.HasPending);
	}

	[Fact]
	public void ApplyIfPending_WithAnUnarmedWorldEntitySource_ReportsOnlyTheHalvesTheRestoreOwes()
	{
		// A layer-end cut drops the entity half BEFORE the audit begins, so this seam
		// must not report a contribution that restore does not owe: the audit raises
		// its report when the count reaches the expectation, and a late extra
		// contribution would raise a SECOND report for a restore already reported.
		var facts = new FakeWorldFactSource();
		var native = new FakeNativeWorldFacts();
		var sink = new FakeRestoredWorldFactSink();
		var entities = new FakeRestoredWorldEntitySource();
		var audit = new WorldRestoreAudit();
		var reports = new List<WorldRestoreLiveWriteReport>();
		audit.Reported += reports.Add;
		audit.BeginRestore("w-layer-end", Attempt);
		var replay = new RestoredWorldFactReplay(
			facts, native, sink, new RecordingLogger<RestoredWorldFactReplay>(), audit, entities);
		facts.ApplyFacts([new BlockStateEntryMsg { X = 1, Y = 2, Block = 0 }], null, Attempt);

		replay.ApplyIfPending();

		Assert.Single(reports);
		Assert.DoesNotContain("apply-world-entities", sink.Calls);
		Assert.Equal(0, entities.Reads);
		Assert.Equal(1, audit.Contributions);
	}

	[Fact]
	public void ApplyIfPending_WorldEntityRowsTheLayerDoesNotHave_ReachTheRestoreAccount()
	{
		// The regenerated layer is expected to hold the identical entity at the
		// identical position, so a refused row is divergence — and it says nothing
		// about the rows of the OTHER half, which really did land.
		var facts = new FakeWorldFactSource();
		var native = new FakeNativeWorldFacts();
		var sink = new FakeRestoredWorldFactSink { RefuseWorldEntities = true };
		var entities = new FakeRestoredWorldEntitySource { Armed = true, Sequence = Attempt, Facts = EntityFacts() };
		var audit = new WorldRestoreAudit();
		var reports = new List<WorldRestoreLiveWriteReport>();
		audit.Reported += reports.Add;
		audit.BeginRestore("w-entities", Attempt, expectedContributions: 2);
		var replay = new RestoredWorldFactReplay(
			facts, native, sink, new RecordingLogger<RestoredWorldFactReplay>(), audit, entities);
		facts.ApplyFacts([new BlockStateEntryMsg { X = 1, Y = 2, Block = 0 }], null, Attempt);

		replay.ApplyIfPending();

		var report = Assert.Single(reports);
		Assert.False(report.Complete);
		Assert.Contains("world-entity", string.Join("; ", report.Refused), StringComparison.Ordinal);
		Assert.Single(entities.Cancels);
		Assert.False(entities.Armed);
		Assert.Equal(0, entities.Commits);

		// The other half is not collateral damage: its rows reached the world, so its
		// handover is finished and its account stays complete.
		Assert.False(facts.HasPendingLiveReplay);
		Assert.False(native.HasPendingRestore);
	}

	[Fact]
	public void ApplyIfPending_MidRunRestore_ReportsTwoHalvesAndWaitsForTheItemReconcile()
	{
		// A mid-run cut owes THREE live-world halves at this seam's end: the world
		// facts, the restored world-entity facts, and the item reconcile that runs one
		// frame later. The report must not be raised from the first two — it would
		// tell the player a restore succeeded while the item half has not run.
		var facts = new FakeWorldFactSource();
		var native = new FakeNativeWorldFacts();
		var sink = new FakeRestoredWorldFactSink();
		var entities = new FakeRestoredWorldEntitySource { Armed = true, Sequence = Attempt, Facts = EntityFacts() };
		var audit = new WorldRestoreAudit();
		var reports = new List<WorldRestoreLiveWriteReport>();
		audit.Reported += reports.Add;
		audit.BeginRestore("w-midrun", Attempt, expectedContributions: 3);
		var replay = new RestoredWorldFactReplay(
			facts, native, sink, new RecordingLogger<RestoredWorldFactReplay>(), audit, entities);
		facts.ApplyFacts([new BlockStateEntryMsg { X = 1, Y = 2, Block = 0 }], null, Attempt);

		replay.ApplyIfPending();

		Assert.Empty(reports);
		Assert.Equal(2, audit.Contributions);
		Assert.Equal(3, audit.ExpectedContributions);
		Assert.True(audit.AwaitingLiveWrite);

		audit.LiveWriteFinished(Attempt, complete: true, refused: [], summary: "the generation reconciled the restored item set (3 entries)");

		var report = Assert.Single(reports);
		Assert.True(report.Complete);
		Assert.Contains("world-entity", report.Summary, StringComparison.Ordinal);
		Assert.Contains("item set", report.Summary, StringComparison.Ordinal);
	}

	[Fact]
	public void ApplyIfPending_WhenTheWriteThrows_ReportsAndReleasesTheWorldEntityHalfToo()
	{
		// The throw path cannot know how far it got, so every half this restore owed
		// reports incomplete and every handover is released — including the entity
		// half, whose write never ran.
		var facts = new FakeWorldFactSource();
		var native = new FakeNativeWorldFacts();
		var sink = new FakeRestoredWorldFactSink { ThrowOnBlockWrite = true };
		var entities = new FakeRestoredWorldEntitySource { Armed = true, Sequence = Attempt, Facts = EntityFacts() };
		var audit = new WorldRestoreAudit();
		var reports = new List<WorldRestoreLiveWriteReport>();
		audit.Reported += reports.Add;
		audit.BeginRestore("w-threw-entities", Attempt, expectedContributions: 2);
		var replay = new RestoredWorldFactReplay(
			facts, native, sink, new RecordingLogger<RestoredWorldFactReplay>(), audit, entities);
		facts.ApplyFacts([new BlockStateEntryMsg { X = 1, Y = 2, Block = 0 }], null, Attempt);

		replay.ApplyIfPending();

		var report = Assert.Single(reports);
		Assert.False(report.Complete);
		Assert.Contains("world-entity", string.Join("; ", report.Refused), StringComparison.Ordinal);
		Assert.DoesNotContain("apply-world-entities", sink.Calls);
		Assert.Single(entities.Cancels);
		Assert.False(entities.Armed);
		Assert.Equal(0, entities.Commits);
		Assert.False(replay.HasPending);
	}

	[Fact]
	public void ApplyIfPending_OnTheGenerationAfterACompletedRestore_ReportsNothing()
	{
		// The world-entry seam calls this once per generation. A normal generation
		// after a completed restore finds nothing pending, and the restore's account is
		// CLOSED: reporting again would tell the player about a restore that is not
		// happening, for a world that is no longer the one being played.
		var facts = new FakeWorldFactSource();
		var native = new FakeNativeWorldFacts();
		var sink = new FakeRestoredWorldFactSink();
		var audit = new WorldRestoreAudit();
		var reports = new List<WorldRestoreLiveWriteReport>();
		audit.Reported += reports.Add;
		audit.BeginRestore("w-generation", Attempt);
		var replay = new RestoredWorldFactReplay(facts, native, sink, new RecordingLogger<RestoredWorldFactReplay>(), audit);
		facts.ApplyFacts([new BlockStateEntryMsg { X = 1, Y = 2, Block = 0 }], null, Attempt);

		replay.ApplyIfPending(); // the restore's own seam
		Assert.Single(reports);
		Assert.False(audit.AwaitingLiveWrite);
		var writes = sink.Calls.Count;

		replay.ApplyIfPending(); // the next generation's seam, with nothing pending

		Assert.Single(reports);
		Assert.Equal(writes, sink.Calls.Count);
		Assert.Equal(1, audit.Contributions);
	}

	private static RestoredWorldEntityFacts EntityFacts() => new(
		[
			new EntityEventMsg
			{
				Kind = EntityEventKind.BearTrapClamped,
				Extra = 3,
				Position = new NetVector2Msg(1.5f, 2.5f),
			},
		],
		[new NetVector2Msg(3.5f, 4.5f)],
		[new BuildingEntityHealthEntryMsg { X = 5.5f, Y = 6.5f, Health = 12f }]);

	private static (RestoredWorldFactReplay Replay, FakeWorldFactSource Facts, FakeNativeWorldFacts Native, FakeRestoredWorldFactSink Sink) Build()
	{
		var facts = new FakeWorldFactSource();
		var native = new FakeNativeWorldFacts();
		var sink = new FakeRestoredWorldFactSink();
		var replay = new RestoredWorldFactReplay(facts, native, sink, new RecordingLogger<RestoredWorldFactReplay>());
		return (replay, facts, native, sink);
	}
}
