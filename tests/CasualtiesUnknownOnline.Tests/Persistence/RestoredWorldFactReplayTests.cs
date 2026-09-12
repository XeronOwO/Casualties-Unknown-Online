using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Persistence;
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
	[Fact]
	public void ApplyIfPending_WithNothingPending_LeavesTheLiveWorldAlone()
	{
		var (replay, _, _, sink) = Build();

		replay.ApplyIfPending();

		Assert.Empty(sink.Calls);
	}

	[Fact]
	public void ApplyIfPending_WhenTheWorldIsNotReady_KeepsTheCutPending()
	{
		var (replay, facts, native, sink) = Build();
		facts.ApplyFacts([new BlockStateEntryMsg { X = 1, Y = 2, Block = 0 }], null);
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
			new RadiationLineStateMsg { Active = true, TimeGone = 1f });
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
		facts.ApplyFacts([new BlockStateEntryMsg { X = 1, Y = 2, Block = 0 }], null);
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
		facts.ApplyFacts([new BlockStateEntryMsg { X = 1, Y = 2, Block = 0 }], null);
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
		facts.ApplyFacts([new BlockStateEntryMsg { X = 1, Y = 2, Block = 0 }], null);

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
		facts.ApplyFacts([new BlockStateEntryMsg { X = 1, Y = 2, Block = 0 }], null);
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
		audit.BeginRestore("w-recipes");
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
		audit.BeginRestore("w-audit");
		var replay = new RestoredWorldFactReplay(facts, native, sink, new RecordingLogger<RestoredWorldFactReplay>(), audit);
		facts.ApplyFacts([new BlockStateEntryMsg { X = 1, Y = 2, Block = 0 }], null);

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
		audit.BeginRestore("w-complete");
		var replay = new RestoredWorldFactReplay(facts, native, sink, new RecordingLogger<RestoredWorldFactReplay>(), audit);
		facts.ApplyFacts([new BlockStateEntryMsg { X = 1, Y = 2, Block = 0 }], null);

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
		audit.BeginRestore("w-threw");
		var replay = new RestoredWorldFactReplay(facts, native, sink, new RecordingLogger<RestoredWorldFactReplay>(), audit);
		facts.ApplyFacts([new BlockStateEntryMsg { X = 1, Y = 2, Block = 0 }], null);

		replay.ApplyIfPending();

		var report = Assert.Single(reports);
		Assert.False(report.Complete);
		Assert.Contains("threw", Assert.Single(report.Refused), StringComparison.Ordinal);
		Assert.False(audit.AwaitingLiveWrite);
		Assert.False(facts.HasPendingLiveReplay);
		Assert.False(replay.HasPending);
	}

	[Fact]
	public void ApplyIfPending_NothingToWrite_CompletesAnAwaitingAudit()
	{
		// A layer-end cut carries no live-world fact, so its restore reaches the
		// world-entry seam with nothing to write. The audit must be told the restore
		// is complete instead of staying armed for a write that will never come.
		var facts = new FakeWorldFactSource();
		var native = new FakeNativeWorldFacts();
		var sink = new FakeRestoredWorldFactSink();
		var audit = new WorldRestoreAudit();
		var reports = new List<WorldRestoreLiveWriteReport>();
		audit.Reported += reports.Add;
		audit.BeginRestore("w-empty");
		var replay = new RestoredWorldFactReplay(facts, native, sink, new RecordingLogger<RestoredWorldFactReplay>(), audit);

		replay.ApplyIfPending();

		var report = Assert.Single(reports);
		Assert.True(report.Complete);
		Assert.Equal("w-empty", report.WorldId);
		Assert.False(audit.AwaitingLiveWrite);
		Assert.Empty(sink.Calls);
	}

	private static (RestoredWorldFactReplay Replay, FakeWorldFactSource Facts, FakeNativeWorldFacts Native, FakeRestoredWorldFactSink Sink) Build()
	{
		var facts = new FakeWorldFactSource();
		var native = new FakeNativeWorldFacts();
		var sink = new FakeRestoredWorldFactSink();
		var replay = new RestoredWorldFactReplay(facts, native, sink, new RecordingLogger<RestoredWorldFactReplay>());
		return (replay, facts, native, sink);
	}
}
