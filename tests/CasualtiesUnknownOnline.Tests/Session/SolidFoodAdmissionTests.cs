using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using CasualtiesUnknownOnline.Tests.Fakes;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The cross-player SOLID-FOOD family's admission rule: which items the one-shot
/// path carries is the item's OWN use-action shape, not a CUO id table. It
/// succeeds the deleted <c>RemoteConsumeCatalog</c> cases — the table carried 25
/// hand-transcribed ids with their hunger/thirst deltas, and its
/// <c>Catalog_ExposesTheCuratedFoodItems</c> case pinned exactly that list. The
/// successor is stronger in the way the migration's whole point demands: the ids
/// below include vanilla edibles the table never carried, and the rule it pins
/// says which shapes the cross-player path can run at all.
/// <para>
/// What no L0 case can reach is the production answer itself — the delegate's
/// compiled body, read by <c>GameSolidFoodFacts</c> — because a test host has no
/// game scene. <see cref="FakeSolidFoodSemantics"/> is the suite's stand-in for
/// it, the same way <c>FakeConsumeSemantics</c> stands in for the game's liquid
/// registry.
/// </para>
/// </summary>
public sealed class SolidFoodAdmissionTests
{
	private static CharacterItemMsg Food(string itemId, float condition = 0.75f) => new()
	{
		InstanceId = 42,
		ItemId = itemId,
		SlotIndex = 0,
		Condition = condition,
	};

	private static bool ActuallyUsable(CharacterItemMsg item) =>
		CarriedItemUseTree.IsActuallyUsable(
			item,
			FakeLimbUseSemantics.Instance,
			FakeConsumeSemantics.Instance,
			FakeWearSemantics.Instance,
			FakeSolidFoodSemantics.Instance);

	[Fact]
	public void TheFamily_IsTheItemsOwnUseActionFeedingABody()
	{
		// The table's own 25 rows stay in the family...
		Assert.True(SolidFoodAdmission.IsFeedable(FakeSolidFoodSemantics.Instance, "bread"));
		Assert.True(SolidFoodAdmission.IsFeedable(FakeSolidFoodSemantics.Instance, "nutrientbar"));
		Assert.True(SolidFoodAdmission.IsFeedable(FakeSolidFoodSemantics.Instance, "stonefruitopen"));

		// ...and the vanilla edibles it never carried are in it now, which is the
		// ceiling this migration removes: a fruit, a mushroom and a meat the table's
		// id list simply did not have.
		Assert.True(SolidFoodAdmission.IsFeedable(FakeSolidFoodSemantics.Instance, "geofruit"));
		Assert.True(SolidFoodAdmission.IsFeedable(FakeSolidFoodSemantics.Instance, "browncap"));
		Assert.True(SolidFoodAdmission.IsFeedable(FakeSolidFoodSemantics.Instance, "internalorgans"));

		// The component-driven can is fed through one call (NonDescriptCan.Eat), which
		// is why the production verdict reads one level of the delegate's own calls.
		Assert.True(SolidFoodAdmission.IsFeedable(FakeSolidFoodSemantics.Instance, "nondescriptcan"));
	}

	[Fact]
	public void AUseActionThatDoesNotFeedABody_IsNotThisFamily()
	{
		// A drink container is the consume chain's (its own LiquidItemInfo.usable), and
		// an item whose use action does something else entirely is nobody's: the
		// deleted table refused these by absence, the item's own action refuses them by
		// shape.
		Assert.Equal(SolidFoodVerdict.NotSolidFood, SolidFoodAdmission.Classify(FakeSolidFoodSemantics.Instance, "waterbottle"));
		Assert.Equal(SolidFoodVerdict.NotSolidFood, SolidFoodAdmission.Classify(FakeSolidFoodSemantics.Instance, "watch"));
		Assert.Equal(SolidFoodVerdict.NotSolidFood, SolidFoodAdmission.Classify(FakeSolidFoodSemantics.Instance, "knife"));
		Assert.False(SolidFoodAdmission.IsFeedable(FakeSolidFoodSemantics.Instance, "knife"));

		// An id the game's registry does not hold at all: no use action, no family.
		Assert.False(SolidFoodAdmission.IsFeedable(FakeSolidFoodSemantics.Instance, "modfood"));
		Assert.False(SolidFoodAdmission.IsFeedable(FakeSolidFoodSemantics.Instance, ""));
	}

	[Fact]
	public void AFoodThatHandsTheEaterAReplacementObject_IsRefusedNotCarried()
	{
		// The last bite of a bucket of chicken instantiates an empty bucket and tries
		// to put it in the eater's hand. On the affected side that object would be
		// created in the EATER's world (a phantom item plus the game's own "too far"
		// alert), and the eater does not own the item, so there is nothing to hand it
		// to: the shape is refused rather than run.
		Assert.Equal(SolidFoodVerdict.EatsAndReplaces, SolidFoodAdmission.Classify(FakeSolidFoodSemantics.Instance, "bucketofchicken"));
		Assert.Equal(SolidFoodVerdict.EatsAndReplaces, SolidFoodAdmission.Classify(FakeSolidFoodSemantics.Instance, "popcorn"));
		Assert.False(SolidFoodAdmission.IsFeedable(FakeSolidFoodSemantics.Instance, "bucketofchicken"));
		Assert.False(SolidFoodAdmission.IsFeedable(FakeSolidFoodSemantics.Instance, "popcorn"));
	}

	[Fact]
	public void AFoodWhoseUseDestroysTheItem_IsCarriedAndItsOwnerLosesIt()
	{
		// exposedcore's action destroys the item object itself, so its condition says
		// nothing about whether it still exists. The shape is read before the eat,
		// because the eater's copy is already gone by the time a report could say so.
		Assert.Equal(SolidFoodVerdict.EatsAndDestroys, SolidFoodAdmission.Classify(FakeSolidFoodSemantics.Instance, "exposedcore"));
		Assert.True(SolidFoodAdmission.IsFeedable(FakeSolidFoodSemantics.Instance, "exposedcore"));
	}

	[Fact]
	public void TheOneShotTree_LeavesTheReplacementShapeOutOfItsReach()
	{
		// The tree's gate is what keeps the host's auto-select from offering an item
		// its own chain must refuse, and what keeps the eater's client from being asked
		// to run an action the family refused.
		Assert.True(ActuallyUsable(Food("bread")));
		Assert.True(ActuallyUsable(Food("exposedcore")));
		Assert.False(ActuallyUsable(Food("bucketofchicken")));
		Assert.False(ActuallyUsable(Food("waterbottle")));
		Assert.False(ActuallyUsable(Food("knife")));

		// A solid food with no condition left is not usable (it is one of the
		// condition-costing families), and neither is any other item's.
		Assert.False(ActuallyUsable(Food("bread", condition: 0f)));
	}

	[Fact]
	public void TheAutoSelect_SkipsTheReplacementShapeAndPicksTheFoodBehindIt()
	{
		var bucket = Food("bucketofchicken");
		bucket.InstanceId = 41;
		var bread = Food("bread");
		bread.InstanceId = 42;

		var picked = CarriedItemUseTree.FindFirstUsable(
			[bucket, bread],
			FakeLimbUseSemantics.Instance,
			FakeConsumeSemantics.Instance,
			FakeWearSemantics.Instance,
			FakeSolidFoodSemantics.Instance);

		Assert.NotNull(picked);
		Assert.Equal(42UL, picked!.InstanceId);

		// Only the refused shape in the inventory: nothing to auto-select, which is a
		// named refusal rather than a request the host would refuse by name afterwards.
		var onlyBucket = CarriedItemUseTree.FindFirstUsable(
			[bucket],
			FakeLimbUseSemantics.Instance,
			FakeConsumeSemantics.Instance,
			FakeWearSemantics.Instance,
			FakeSolidFoodSemantics.Instance);
		Assert.Null(onlyBucket);
	}
}
