using System;
using System.Collections.Generic;
using System.Reflection;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Patching;

/// <summary>
/// The drop table the host bounds a member's break report against
/// (GameBlockDropTable), read out of the REAL adapter assembly: the game's own
/// loot switch arm for arm, and a custom tile's authored drops in the shape its
/// spawner can produce them. What is verified here is that mirror plus the
/// bound's verdicts over it.
///
/// What is NOT verifiable here, and is named as such:
/// - the binding to the GAME BUILD: the switch is code
///   (<c>WorldGeneration.DamageBlock</c>), not data, so a game update that moves
///   an arm cannot be caught by a test that does not parse the assembly's IL.
///   What makes such a drift diagnosable rather than silent is the refusal line,
///   which names the block id and the drops it could not produce;
/// - the live path — a member's forged report refused on the host and its drops
///   rolled back — which needs the running game and is read by the acceptance
///   batch (<c>docs/acceptance/</c>), not here.
/// </summary>
[Collection(GameAssemblyCollection.Name)]
[Trait("Category", "Integration")]
public class GameBlockDropTableTests
{
	[Fact]
	public void ForVanilla_NamesTheArmTheGameRollsForEachBlock()
	{
		AssertRows(ForVanilla(3), ("scrapmetal", 0.05f, 0.2f));
		AssertRows(ForVanilla(6), ("scrapmetal", 0.5f, 1f));
		AssertRows(ForVanilla(10), ("scrapmetal", 0.5f, 1f));
		AssertRows(ForVanilla(7), ("glassshards", null, null));
		AssertRows(ForVanilla(8), ("plasticchunk", null, null));
		AssertRows(ForVanilla(9), ("plasticchunk", null, null));
		AssertRows(ForVanilla(34), ("rawcopper", null, null));
		AssertRows(ForVanilla(35), ("ilmenitechunk", null, null));
	}

	[Fact]
	public void ForVanilla_TheWoodArm_IsOneRollOverThreeIds()
	{
		// Random.Range(0, 3) creates ONE of the three. Spelled as three rows the
		// table would accept all three from a single break; as one row it accepts
		// one of them and refuses the second claim.
		foreach (var block in new ushort[] { 11, 24 })
		{
			var rows = ForVanilla(block);
			var row = Assert.Single(rows);
			Assert.Equal(["woodscraps", "stick", "woodpanel"], row.ItemIds);
			Assert.Null(row.MinCondition);
			Assert.Null(row.MaxCondition);
		}

		var outcome = DropBoundPolicy.Judge(ForVanilla(11), [Drop("woodscraps"), Drop("stick")]);
		Assert.Single(outcome.Accepted);
		Assert.Single(outcome.Refused);
	}

	[Fact]
	public void ForVanilla_ABlockTheGameDropsNothingFor_RowsNothing()
	{
		// The switch's no-loot path: every other vanilla id (and block 27, whose
		// arm writes water into the cell instead of creating an item), plus the
		// custom index range, which the game's switch never reaches.
		foreach (var block in new ushort[] { 0, 1, 2, 4, 5, 12, 13, 23, 25, 26, 27, 28, 33, 36, 64 })
		{
			Assert.Empty(ForVanilla(block));
		}

		Assert.Single(DropBoundPolicy.Judge(ForVanilla(1), [Drop("rawcopper")]).Refused);
	}

	[Fact]
	public void ForVanilla_EnforcesTheDeclaredWindow_AndChecksNoConditionWithoutOne()
	{
		// Block 3's arm is Random.Range(0.05f, 0.2f): both ends are legitimate rolls.
		var lowCondition = ForVanilla(3);
		Assert.Single(DropBoundPolicy.Judge(lowCondition, [Drop("scrapmetal", 0.05f)]).Accepted);
		Assert.Single(DropBoundPolicy.Judge(lowCondition, [Drop("scrapmetal", 0.2f)]).Accepted);
		Assert.Single(DropBoundPolicy.Judge(lowCondition, [Drop("scrapmetal", 0.9f)]).Refused);

		// Block 6's arm is Random.Range(0.5f, 1f) — the same id, a different window,
		// which is why the window belongs to the ROW and not to the item id.
		Assert.Single(DropBoundPolicy.Judge(ForVanilla(6), [Drop("scrapmetal", 0.6f)]).Accepted);
		Assert.Single(DropBoundPolicy.Judge(ForVanilla(6), [Drop("scrapmetal", 0.2f)]).Refused);

		// No window declared (the arm leaves the item's own default standing): the
		// condition says nothing, so nothing is refused on it.
		Assert.Single(DropBoundPolicy.Judge(ForVanilla(7), [Drop("glassshards", 0f)]).Accepted);
		Assert.Single(DropBoundPolicy.Judge(ForVanilla(7), [Drop("glassshards", 1f)]).Accepted);
	}

	[Fact]
	public void ForTile_TakesTheAuthoredRows_AndSkipsWhatTheSpawnerCanNeverRoll()
	{
		var definition = new ModTileDefinition
		{
			Id = "mod:test-tile",
			Drops =
			[
				new ModTileDrop { ItemId = "scrapmetal", Chance = 1f, MinCondition = 0.2f, MaxCondition = 0.4f },
				new ModTileDrop { ItemId = "never", Chance = 0f, MinCondition = 0f, MaxCondition = 1f },
				new ModTileDrop { ItemId = "", Chance = 1f, MinCondition = 0f, MaxCondition = 1f },
				null!,
			],
		};

		var rows = ForTile(definition);

		var row = Assert.Single(rows);
		Assert.Equal(["scrapmetal"], row.ItemIds);
		Assert.Equal(0.2f, row.MinCondition);
		Assert.Equal(0.4f, row.MaxCondition);

		// The window is the spawner's own clamping: Clamp(Random.Range(0f, 1f), min, max).
		Assert.Single(DropBoundPolicy.Judge(rows, [Drop("scrapmetal", 0.3f)]).Accepted);
		Assert.Single(DropBoundPolicy.Judge(rows, [Drop("scrapmetal", 0.5f)]).Refused);
		Assert.Single(DropBoundPolicy.Judge(rows, [Drop("never", 0.5f)]).Refused);
		Assert.Single(DropBoundPolicy.Judge(rows, [Drop("stick", 0.3f)]).Refused);
	}

	[Fact]
	public void ForTile_NoAuthoredDrops_RowsNothing() =>
		Assert.Empty(ForTile(new ModTileDefinition { Id = "mod:empty" }));

	// ---- the adapter methods (reflection: the test project never compile-references the adapter assembly) ----

	private static Type TableType() =>
		GameAssemblyHost.Adapter.GetType("CasualtiesUnknownOnline.GameAdapter.World.GameBlockDropTable")
		?? throw new InvalidOperationException("GameBlockDropTable not found in the GameAdapter assembly.");

	private static MethodInfo TableMethod(string name) =>
		TableType().GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
		?? throw new InvalidOperationException($"GameBlockDropTable.{name} not found.");

	private static IReadOnlyList<DropBound> ForVanilla(ushort block) =>
		(IReadOnlyList<DropBound>)TableMethod("ForVanilla").Invoke(null, [block])!;

	private static IReadOnlyList<DropBound> ForTile(IModTileDefinition definition) =>
		(IReadOnlyList<DropBound>)TableMethod("ForTile").Invoke(null, [definition])!;

	private static void AssertRows(IReadOnlyList<DropBound> rows, params (string ItemId, float? Min, float? Max)[] expected)
	{
		Assert.Equal(expected.Length, rows.Count);
		for (var i = 0; i < expected.Length; i++)
		{
			Assert.Equal([expected[i].ItemId], rows[i].ItemIds);
			Assert.Equal(expected[i].Min, rows[i].MinCondition);
			Assert.Equal(expected[i].Max, rows[i].MaxCondition);
		}
	}

	private static BlockDropEntryMsg Drop(string itemId, float condition = 1f) =>
		new() { Item = new CharacterItemMsg { ItemId = itemId, Condition = condition } };
}
