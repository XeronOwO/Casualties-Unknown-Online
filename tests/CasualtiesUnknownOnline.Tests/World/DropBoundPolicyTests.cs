using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The host's bound on a member's destruction report (DropBoundPolicy): the
/// drops a break carries are the breaker's local roll, the host cannot re-derive
/// that roll (the game draws it from the process-global unseeded
/// UnityEngine.Random), so the report is bounded against the destroyed thing's
/// own table instead. What is verified here is the second half of that rule —
/// which reported drops some table row can account for and which none can — over
/// the shapes the two real tables produce: a chance-gated single id, a condition
/// window, and the one arm that names three ids in a single roll.
/// </summary>
public class DropBoundPolicyTests
{
	private const string ScrapMetal = "scrapmetal";

	[Fact]
	public void DropsInsideTheBounds_AreAccepted()
	{
		var outcome = DropBoundPolicy.Judge([Row(ScrapMetal, 0.05f, 0.2f)], [Drop(ScrapMetal, 0.1f)]);

		Assert.Single(outcome.Accepted);
		Assert.Empty(outcome.Refused);
		Assert.False(outcome.AnyRefused);
	}

	[Fact]
	public void AnItemTheTableDoesNotName_IsRefused()
	{
		// Block 34's arm creates rawcopper and nothing else: scrapmetal is not a
		// drop that block can produce.
		var outcome = DropBoundPolicy.Judge([Row("rawcopper")], [Drop(ScrapMetal, 1f)]);

		Assert.Empty(outcome.Accepted);
		Assert.Single(outcome.Refused);
		Assert.True(outcome.AnyRefused);
	}

	[Fact]
	public void AConditionOutsideTheDeclaredWindow_IsRefused()
	{
		// The game rolls this row as Random.Range(0.05f, 0.2f): a report claiming
		// full condition is not a roll this arm can produce.
		Assert.Single(DropBoundPolicy.Judge([Row(ScrapMetal, 0.05f, 0.2f)], [Drop(ScrapMetal, 1f)]).Refused);
		Assert.Single(DropBoundPolicy.Judge([Row(ScrapMetal, 0.05f, 0.2f)], [Drop(ScrapMetal, 0.01f)]).Refused);
	}

	[Fact]
	public void EitherEdgeOfTheWindow_IsAccepted()
	{
		// A legitimate roll must never be refused: Random.Range includes both ends.
		var bounds = new List<DropBound> { Row(ScrapMetal, 0.05f, 0.2f) };

		Assert.Single(DropBoundPolicy.Judge(bounds, [Drop(ScrapMetal, 0.05f)]).Accepted);
		Assert.Single(DropBoundPolicy.Judge(bounds, [Drop(ScrapMetal, 0.2f)]).Accepted);
	}

	[Fact]
	public void TheSlack_CoversAFloatRounding_ButNotAConditionTheRollCannotReach()
	{
		var bounds = new List<DropBound> { Row(ScrapMetal, 0.05f, 0.2f) };

		Assert.Single(DropBoundPolicy.Judge(bounds, [Drop(ScrapMetal, 0.0495f)]).Accepted);
		Assert.Single(DropBoundPolicy.Judge(bounds, [Drop(ScrapMetal, 0.04f)]).Refused);
	}

	[Fact]
	public void OneRowIsOneRoll_ASecondDropOfTheSameIdIsRefused()
	{
		// The arm rolls ONCE: two scrapmetal items cannot come from one break of
		// block 3, and the second one is what the bound is there to catch.
		var outcome = DropBoundPolicy.Judge([Row(ScrapMetal, 0.05f, 0.2f)], [Drop(ScrapMetal, 0.1f), Drop(ScrapMetal, 0.15f)]);

		Assert.Single(outcome.Accepted);
		Assert.Single(outcome.Refused);
	}

	[Fact]
	public void TheWoodArm_IsOneRowOverThreeIds_SoOneBreakYieldsOneOfThem()
	{
		// The game's wood arm is Random.Range(0, 3) over three ids: it is ONE row.
		var bounds = new List<DropBound> { new(["woodscraps", "stick", "woodpanel"]) };

		Assert.Single(DropBoundPolicy.Judge(bounds, [Drop("stick")]).Accepted);
		Assert.Single(DropBoundPolicy.Judge(bounds, [Drop("woodpanel")]).Accepted);

		var both = DropBoundPolicy.Judge(bounds, [Drop("woodscraps"), Drop("stick")]);
		Assert.Single(both.Accepted);
		Assert.Single(both.Refused);
	}

	[Fact]
	public void ARowWithoutAWindow_ChecksNoCondition()
	{
		// The game assigns no condition for these arms (the item's own default
		// stands), so there is nothing to verify — recorded here as the bound's
		// deliberate half-open side rather than left implicit.
		var bounds = new List<DropBound> { new(["glassshards"]) };

		Assert.Single(DropBoundPolicy.Judge(bounds, [Drop("glassshards", 0f)]).Accepted);
		Assert.Single(DropBoundPolicy.Judge(bounds, [Drop("glassshards", 1f)]).Accepted);
	}

	[Fact]
	public void AnEmptyBoundSet_RefusesEveryDrop()
	{
		// A block the game's own switch sends to the no-loot path (and a custom
		// tile this side has no definition for): it can produce nothing.
		var outcome = DropBoundPolicy.Judge([], [Drop(ScrapMetal, 1f), Drop("glassshards", 1f)]);

		Assert.Empty(outcome.Accepted);
		Assert.Equal(2, outcome.Refused.Count);
	}

	[Fact]
	public void AMissingDrop_IsNeverRefused()
	{
		// The bound is one-sided on purpose: a drop the report does not carry (a
		// row that rolled nothing, a creation the report never folded in) is the
		// breaker's own loss, never a refusal.
		var bounds = new List<DropBound> { Row(ScrapMetal, 0.05f, 0.2f), new(["glassshards"]) };

		var none = DropBoundPolicy.Judge(bounds, []);
		Assert.Empty(none.Accepted);
		Assert.Empty(none.Refused);

		var fewer = DropBoundPolicy.Judge(bounds, [Drop(ScrapMetal, 0.1f)]);
		Assert.Single(fewer.Accepted);
		Assert.Empty(fewer.Refused);
	}

	[Fact]
	public void ADropWithNoItemState_IsRefused()
	{
		// Nothing to materialize and nothing to name: it cannot be a roll's product.
		var outcome = DropBoundPolicy.Judge([Row(ScrapMetal)], [new BlockDropEntryMsg { ItemId = 7 }, new BlockDropEntryMsg { ItemId = 8, Item = new CharacterItemMsg { ItemId = "" } }]);

		Assert.Empty(outcome.Accepted);
		Assert.Equal(2, outcome.Refused.Count);
	}

	[Fact]
	public void TheOutcome_PartitionsTheReport()
	{
		var drops = new List<BlockDropEntryMsg> { Drop(ScrapMetal, 0.1f), Drop("ilmenitechunk"), Drop("rawcopper") };

		var outcome = DropBoundPolicy.Judge([Row(ScrapMetal, 0.05f, 0.2f), new(["rawcopper"])], drops);

		Assert.Equal(drops.Count, outcome.Accepted.Count + outcome.Refused.Count);
		Assert.Equal(
			drops.Select(drop => drop.ItemId).OrderBy(id => id),
			outcome.Accepted.Concat(outcome.Refused).Select(drop => drop.ItemId).OrderBy(id => id));
	}

	[Fact]
	public void TwoRowsOverTheSameId_RefuseNothingWhileTheReportStillFits()
	{
		// A custom tile may author the same item twice with different windows. Read in
		// report order with the WIDE row first, a per-drop greedy match would let the
		// 0.95 drop take the wide row and then refuse the 0.3 drop — which the wide row
		// could have produced. The rows are matched as a SET, so both fit.
		var bounds = new List<DropBound> { new([ScrapMetal], 0f, 1f), new([ScrapMetal], 0.9f, 1f) };

		var outcome = DropBoundPolicy.Judge(bounds, [Drop(ScrapMetal, 0.95f), Drop(ScrapMetal, 0.3f)]);

		Assert.Equal(2, outcome.Accepted.Count);
		Assert.Empty(outcome.Refused);
	}

	[Fact]
	public void OneRowTooFew_StillRefusesExactlyOne()
	{
		// The matching moves drops between rows; it does not invent rows. Three drops
		// over two rows leave exactly one refused, whichever one that is.
		var bounds = new List<DropBound> { new([ScrapMetal], 0f, 1f), new([ScrapMetal], 0.5f, 1f) };

		var outcome = DropBoundPolicy.Judge(bounds, [Drop(ScrapMetal, 0.95f), Drop(ScrapMetal, 0.6f), Drop(ScrapMetal, 0.55f)]);

		Assert.Equal(2, outcome.Accepted.Count);
		Assert.Single(outcome.Refused);
	}

	[Fact]
	public void TheBuildingDeathShape_IsJudgedByTheSameRule()
	{
		// The building-death drops ride the same break report and answer to the same
		// bound, so they go through the same matcher: a drop a row of the dying
		// entity's table can produce is kept, one no row can produce is refused, and
		// a row still accounts for exactly one of them.
		var bounds = new List<DropBound> { Row(ScrapMetal, 0.5f, 1f) };

		Assert.Single(DropBoundPolicy.JudgeBuildingDrops(bounds, [TrapDrop(ScrapMetal, 0.5f)]).Accepted);
		Assert.Single(DropBoundPolicy.JudgeBuildingDrops(bounds, [TrapDrop(ScrapMetal, 0.4f)]).Refused);
		Assert.Single(DropBoundPolicy.JudgeBuildingDrops(bounds, [TrapDrop("rawcopper")]).Refused);

		var two = DropBoundPolicy.JudgeBuildingDrops(bounds, [TrapDrop(ScrapMetal, 0.5f), TrapDrop(ScrapMetal, 0.6f)]);
		Assert.Single(two.Accepted);
		Assert.Single(two.Refused);
	}

	[Fact]
	public void ABuildingDropWithNoItemState_IsRefused()
	{
		// Same rule on the other shape: nothing to materialize and nothing to name.
		var outcome = DropBoundPolicy.JudgeBuildingDrops([Row(ScrapMetal)], [new TrapDropEntryMsg { ItemId = 9 }]);

		Assert.Empty(outcome.Accepted);
		Assert.Single(outcome.Refused);
	}

	private static DropBound Row(string itemId, float? min = null, float? max = null) => new([itemId], min, max);

	private int _instanceId;

	private BlockDropEntryMsg Drop(string itemId, float condition = 1f) =>
		new()
		{
			ItemId = (ulong)++_instanceId,
			Item = new CharacterItemMsg { ItemId = itemId, Condition = condition },
		};

	private TrapDropEntryMsg TrapDrop(string itemId, float condition = 1f) =>
		new()
		{
			ItemId = (ulong)++_instanceId,
			Item = new CharacterItemMsg { ItemId = itemId, Condition = condition },
		};
}
