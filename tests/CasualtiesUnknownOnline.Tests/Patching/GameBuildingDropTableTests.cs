using System;
using System.Collections.Generic;
using System.Reflection;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The third drop table the host bounds a member's break report against
/// (GameBuildingDropTable), read out of the REAL adapter assembly: a building
/// entity's own prefab data — the chance-gated <c>itemsDropOnDestroy</c> bucket,
/// the ungated <c>alwaysDrop</c> one, and the <c>guaranteedDropAmount</c> rolls
/// over <c>ItemLootPool</c> categories — resolved into the rows the bound is
/// judged over.
///
/// What is NOT verifiable here, and is named as such:
/// - the LIVE read (the <c>ItemDrop[]</c> arrays and the pool dictionary off a
///   running entity): the test drives the same arithmetic over plain data, and the
///   projection from the component is exercised by the acceptance batch on a real
///   support-loss death;
/// - the CAPTURE POINT — that the host reads these rows while the entity still
///   exists, from the support-loss mark, before the report arrives — which is
///   <c>BlockBreakSync</c>'s half and runs against the live world.
/// </summary>
[Collection(GameAssemblyCollection.Name)]
[Trait("Category", "Integration")]
public class GameBuildingDropTableTests
{
	[Fact]
	public void TheChanceGatedBucket_IsOneRollPerEntry_AndSkipsWhatTheGateCanNeverPass()
	{
		var rows = RowsFor(
			droppedOnDestroy:
			[
				new AuthoredDrop("glassshards", 0.5f, 0.1f, 0.4f),
				new AuthoredDrop("scrapmetal", 0f, 0f, 1f),
				new AuthoredDrop("", 1f, 0f, 1f),
			],
			alwaysDropped: [],
			guaranteedDropAmount: 0,
			categoriesToAdd: null,
			dropChanceMultiplier: 1f,
			lootPool: null);

		var row = Assert.Single(rows);
		Assert.Equal(["glassshards"], row.ItemIds);
		Assert.Equal(0.1f, row.MinCondition);
		Assert.Equal(0.4f, row.MaxCondition);
	}

	[Fact]
	public void TheChanceGatedBucket_ReadsTheMultiplier()
	{
		// The gate is chance * dropChanceMultiplier, so the multiplier is what decides
		// whether a row can ever pass: at zero every entry is unreachable.
		var entries = new List<AuthoredDrop> { new("glassshards", 0.5f, 0f, 1f) };

		Assert.Single(RowsFor(entries, [], 0, null, dropChanceMultiplier: 1f, lootPool: null));
		Assert.Empty(RowsFor(entries, [], 0, null, dropChanceMultiplier: 0f, lootPool: null));
	}

	[Fact]
	public void TheAlwaysDropBucket_IsOneUngatedRollPerEntry_AndIgnoresTheChance()
	{
		// The game reads no chance in this loop: an entry with chance 0 still creates
		// its item, so it still bounds one.
		var rows = RowsFor(
			droppedOnDestroy: [],
			alwaysDropped: [new AuthoredDrop("stick", 0f, 1f, 0.2f), new AuthoredDrop("", 1f, 0f, 1f)],
			guaranteedDropAmount: 0,
			categoriesToAdd: null,
			dropChanceMultiplier: 1f,
			lootPool: null);

		var row = Assert.Single(rows);
		Assert.Equal(["stick"], row.ItemIds);
		// Random.Range swaps its bounds when the minimum is the greater one, so a row
		// authored backwards is the same window, not a refusal.
		Assert.Equal(0.2f, row.MinCondition);
		Assert.Equal(1f, row.MaxCondition);
	}

	[Fact]
	public void TheGuaranteedRolls_AreOneRowEach_OverEveryNamedCategorysPool()
	{
		var pool = new Dictionary<string, List<string>>(StringComparer.Ordinal)
		{
			["metal"] = ["scrapmetal", "rawcopper"],
			["wood"] = ["stick", "scrapmetal"],
		};

		var rows = RowsFor([], [], guaranteedDropAmount: 2, categoriesToAdd: ["metal", "wood"], dropChanceMultiplier: 1f, lootPool: pool);

		Assert.Equal(2, rows.Count);
		foreach (var row in rows)
		{
			// ONE roll picks a category and then an entry, so the row names the union
			// of every named category (deduplicated) at the exactly-1 condition the
			// game writes — never one row per category.
			Assert.Equal(["scrapmetal", "rawcopper", "stick"], row.ItemIds);
			Assert.Equal(1f, row.MinCondition);
			Assert.Equal(1f, row.MaxCondition);
		}
	}

	[Fact]
	public void TheGuaranteedRolls_ContributeNothing_ThePoolCannotName()
	{
		var pool = new Dictionary<string, List<string>>(StringComparer.Ordinal) { ["metal"] = ["scrapmetal"] };

		// An unknown category is one the game's own indexer would throw on, an empty
		// list names nothing, and a missing pool cannot answer at all: no roll of any
		// of them can create an item, so none of them bounds one.
		Assert.Empty(RowsFor([], [], 3, ["unknown"], 1f, pool));
		Assert.Empty(RowsFor([], [], 3, [], 1f, pool));
		Assert.Empty(RowsFor([], [], 3, null, 1f, pool));
		Assert.Empty(RowsFor([], [], 3, ["metal"], 1f, null));

		// A negative amount is no roll either.
		Assert.Empty(RowsFor([], [], -1, ["metal"], 1f, pool));
	}

	[Fact]
	public void TheRows_AreWhatTheBoundJudgesADeathReportOver()
	{
		var rows = RowsFor(
			droppedOnDestroy: [new AuthoredDrop("scrapmetal", 1f, 0.5f, 1f)],
			alwaysDropped: [new AuthoredDrop("stick", 0f, 1f, 1f)],
			guaranteedDropAmount: 0,
			categoriesToAdd: null,
			dropChanceMultiplier: 1f,
			lootPool: null);

		Assert.Equal(2, rows.Count);
		Assert.Single(DropBoundPolicy.JudgeBuildingDrops(rows, [TrapDrop("scrapmetal", 0.7f)]).Accepted);
		Assert.Single(DropBoundPolicy.JudgeBuildingDrops(rows, [TrapDrop("scrapmetal", 0.2f)]).Refused);
		Assert.Single(DropBoundPolicy.JudgeBuildingDrops(rows, [TrapDrop("stick", 1f)]).Accepted);
		Assert.Single(DropBoundPolicy.JudgeBuildingDrops(rows, [TrapDrop("glassshards", 1f)]).Refused);
	}

	// ---- the adapter method (reflection: the test project never compile-references the adapter assembly) ----

	private static IReadOnlyList<DropBound> RowsFor(
		IReadOnlyList<AuthoredDrop> droppedOnDestroy,
		IReadOnlyList<AuthoredDrop> alwaysDropped,
		int guaranteedDropAmount,
		IReadOnlyList<string>? categoriesToAdd,
		float dropChanceMultiplier,
		IReadOnlyDictionary<string, List<string>>? lootPool)
	{
		var type = GameAssemblyHost.Adapter.GetType("CasualtiesUnknownOnline.GameAdapter.World.GameBuildingDropTable")
			?? throw new InvalidOperationException("GameBuildingDropTable not found in the GameAdapter assembly.");
		var method = type.GetMethod("RowsFor", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("GameBuildingDropTable.RowsFor not found.");

		return (IReadOnlyList<DropBound>)method.Invoke(null, [droppedOnDestroy, alwaysDropped, guaranteedDropAmount, categoriesToAdd, dropChanceMultiplier, lootPool])!;
	}

	private static TrapDropEntryMsg TrapDrop(string itemId, float condition) =>
		new() { Item = new CharacterItemMsg { ItemId = itemId, Condition = condition } };
}
