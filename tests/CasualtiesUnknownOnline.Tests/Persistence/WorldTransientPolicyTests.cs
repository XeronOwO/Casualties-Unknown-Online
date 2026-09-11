using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Session.Persistence;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Persistence;

/// <summary>
/// The cut's transient policy table: every in-flight class the ticket names has
/// exactly one verdict, and the verdicts a cut acts on are pinned here. The table
/// is the ONLY place a verdict is decided — this suite is what stops a new
/// in-flight state from being added without one.
/// </summary>
public class WorldTransientPolicyTests
{
	/// <summary>The ticket's table, one key per in-flight class (todo/save-mid-run-consistent-cut.md §3).</summary>
	private static readonly string[] TicketRows =
	[
		"block-break-pending",
		"trap-drop-hold",
		"pickup-queue",
		"drop-flush",
		"medical-session",
		"shrapnel-session",
		"other-medical-session",
		"craft-batch",
		"deferred-entity-report",
		"item-physics",
		"decided-native-values",
		"world-clock",
		"earthquake-timers",
	];

	[Fact]
	public void Rows_CoverEveryInFlightClassTheTicketNames() =>
		Assert.Equal(TicketRows, WorldTransientPolicy.Rows.Select(row => row.Key));

	[Fact]
	public void Rows_AreUniqueAndEveryOneCarriesItsOwnerUnitAndReason()
	{
		Assert.Equal(TicketRows.Length, WorldTransientPolicy.Rows.Select(row => row.Key).Distinct().Count());
		foreach (var row in WorldTransientPolicy.Rows)
		{
			Assert.False(string.IsNullOrWhiteSpace(row.Owner), row.Key);
			Assert.False(string.IsNullOrWhiteSpace(row.Unit), row.Key);
			Assert.False(string.IsNullOrWhiteSpace(row.Note), row.Key);
		}
	}

	[Fact]
	public void Verdicts_PerRow_AreTheDecidedOnes()
	{
		// The decision, spelled out per row. Resolve-before-save is only for the
		// states whose world effect reaches the kernel through the same flush they
		// wait for; the decided native values are the one captured class; everything
		// else is dropped WITH its name in the report.
		var expected = new Dictionary<string, WorldTransientVerdict>
		{
			["block-break-pending"] = WorldTransientVerdict.ResolveBeforeSave,
			["trap-drop-hold"] = WorldTransientVerdict.ResolveBeforeSave,
			["pickup-queue"] = WorldTransientVerdict.DropWithLog,
			["drop-flush"] = WorldTransientVerdict.ResolveBeforeSave,
			["medical-session"] = WorldTransientVerdict.DropWithLog,
			["shrapnel-session"] = WorldTransientVerdict.DropWithLog,
			["other-medical-session"] = WorldTransientVerdict.DropWithLog,
			["craft-batch"] = WorldTransientVerdict.DropWithLog,
			["deferred-entity-report"] = WorldTransientVerdict.DropWithLog,
			["item-physics"] = WorldTransientVerdict.DropWithLog,
			["decided-native-values"] = WorldTransientVerdict.Capture,
			["world-clock"] = WorldTransientVerdict.DropWithLog,
			["earthquake-timers"] = WorldTransientVerdict.DropWithLog,
		};

		Assert.Equal(expected, WorldTransientPolicy.Rows.ToDictionary(row => row.Key, row => row.Verdict));
	}

	[Fact]
	public void Detection_DeclaresTheRowsNoObserverCanCount()
	{
		// A row marked Standing is named WITHOUT a count by every cut (the game owns
		// the state). A row that claims Standing while an owner actually reports it —
		// or the reverse — would make the report claim less, or more, than CUO knows.
		var standing = WorldTransientPolicy.Rows
			.Where(row => row.Detection == WorldTransientDetection.Standing)
			.Select(row => row.Key)
			.ToList();

		Assert.Equal(
			[
				WorldTransientPolicy.CraftBatchKey,
				WorldTransientPolicy.ItemPhysicsKey,
				WorldTransientPolicy.WorldClockKey,
				WorldTransientPolicy.EarthquakeTimersKey,
			],
			standing);
	}

	[Fact]
	public void MustResolveBeforeSave_MatchesTheVerdict()
	{
		Assert.True(WorldTransientPolicy.MustResolveBeforeSave(WorldTransientPolicy.BlockBreakPendingKey));
		Assert.True(WorldTransientPolicy.MustResolveBeforeSave(WorldTransientPolicy.TrapDropHoldKey));
		Assert.True(WorldTransientPolicy.MustResolveBeforeSave(WorldTransientPolicy.DropFlushKey));
		Assert.False(WorldTransientPolicy.MustResolveBeforeSave(WorldTransientPolicy.PickupQueueKey));
		Assert.False(WorldTransientPolicy.MustResolveBeforeSave(WorldTransientPolicy.DecidedNativeValuesKey));
	}

	[Fact]
	public void Find_UnknownKeyIsNotDeclared()
	{
		Assert.Null(WorldTransientPolicy.Find("no-such-class"));
		Assert.False(WorldTransientPolicy.IsKnown("no-such-class"));
		Assert.False(WorldTransientPolicy.MustResolveBeforeSave("no-such-class"));
	}

	[Fact]
	public void Describe_NamesTheCountAndTheUnit()
	{
		Assert.Equal(
			"2 pickup claim(s) waiting for their spawn report",
			WorldTransientPolicy.Describe(new WorldTransientCount(WorldTransientPolicy.PickupQueueKey, 2)));
	}

	[Fact]
	public void Describe_UnknownKeyStillNamesTheClass() =>
		// The report must never swallow an owner bug into a generic "some state".
		Assert.Equal("3 unknown transient class 'ghost-class'", WorldTransientPolicy.Describe(new WorldTransientCount("ghost-class", 3)));
}
