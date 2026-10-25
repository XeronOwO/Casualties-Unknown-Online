using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using CasualtiesUnknownOnline.Tests.Fakes;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The ONE family order, asked per ENTRY: the host's dispatch, the world-drag gate
/// and the medical view's dose measurement all read this answer, so an item the
/// game's data puts in two families is routed by the gesture the player made rather
/// than by the order the rules happen to sit in (decision 246). The fake seams stand
/// in for the game's registries; the live answer comes from
/// <c>GameLimbUseFacts</c>/<c>GameConsumeFacts</c>/the wearable and food facts,
/// which no test host can load.
/// </summary>
public sealed class ItemUseFamilyClassifierTests
{
	private static ItemUseFamily Classify(ItemUseEntry entry, string itemId, params (string LiquidId, float Amount)[] liquids)
	{
		var stacks = new List<LiquidStackMsg>(liquids.Length);
		foreach (var liquid in liquids)
		{
			stacks.Add(new LiquidStackMsg { LiquidId = liquid.LiquidId, Amount = liquid.Amount });
		}

		return ItemUseFamilyClassifier.Classify(
			entry,
			itemId,
			stacks,
			FakeLimbUseSemantics.Instance,
			FakeConsumeSemantics.Instance,
			FakeWearSemantics.Instance,
			FakeSolidFoodSemantics.Instance);
	}

	[Fact]
	public void TheDrinkableBagHoldingAHealthUsableLiquid_IsTheInventoryDrinkAndTheLimbInjection()
	{
		// The container this rule was extracted for, resolved by ticket `topical-limb-action-shape.md`: `saline` is
		// usable AND usableOnLimb and its own use action drinks, so the INVENTORY entry is a drink — while the WOUND
		// VIEW's release runs the item's LIMB action, which for this bag is the injection flow. Native
		// `PlayerCamera.ApplyWoundItem` runs that action whatever the player filled the bag with, so the limb entry is the
		// injection family (the operator's client starts it and the host arbitrates), not a pour and not a refusal.
		Assert.Equal(ItemUseFamily.Drink, Classify(ItemUseEntry.Inventory, "saline", ("disinfectant", 750f)));
		Assert.Equal(ItemUseFamily.Injection, Classify(ItemUseEntry.Limb, "saline", ("disinfectant", 750f)));
	}

	[Fact]
	public void AContainerHoldingAnInjectableLiquid_StaysTheInjectionRefusalOnBothEntries()
	{
		// The injection rule is asked first on BOTH entries, before the drink and
		// before the topical rule: a container the game can also fill with an
		// injectable liquid may not reach the drink measurement, whose native action
		// for a blood bag draws blood into the acting player's bag.
		Assert.Equal(ItemUseFamily.Injection, Classify(ItemUseEntry.Inventory, "saline", ("saline", 750f)));
		Assert.Equal(ItemUseFamily.Injection, Classify(ItemUseEntry.Limb, "saline", ("saline", 750f)));

		// The limb carriers' own mixed shape is the same answer: ANY liquid with the
		// injectable flag owns the container.
		Assert.Equal(ItemUseFamily.Injection, Classify(ItemUseEntry.Inventory, "paincream", ("reliefcream", 90f), ("fentanyl", 10f)));
		Assert.Equal(ItemUseFamily.Injection, Classify(ItemUseEntry.Limb, "paincream", ("reliefcream", 90f), ("fentanyl", 10f)));
	}

	[Fact]
	public void ATopicalCarrier_IsAbsentFromTheInventoryEntry()
	{
		// A cream is a limb carrier whose item data is not `usable`: the inventory use
		// has no action for it at all (`Body.UseItem` gates on `usable`), so the world
		// drag must fall through to the native drop rather than send a dose.
		Assert.Equal(ItemUseFamily.None, Classify(ItemUseEntry.Inventory, "paincream", ("reliefcream", 100f)));
		Assert.Equal(ItemUseFamily.Topical, Classify(ItemUseEntry.Limb, "paincream", ("reliefcream", 100f)));
	}

	[Fact]
	public void ADrinkContainer_IsTheDrinkOnBothEntries()
	{
		// The ordinary drink: the inventory use's own action, and the held-remote
		// release's fallback when the wound view runs it against the acting body.
		Assert.Equal(ItemUseFamily.Drink, Classify(ItemUseEntry.Inventory, "waterbottle", ("water", 500f)));
		Assert.Equal(ItemUseFamily.Drink, Classify(ItemUseEntry.Limb, "waterbottle", ("water", 500f)));
	}

	[Fact]
	public void AUsableContainerThatIsNotLimbDrawable_CannotBecomeTheInjectionFamilyByItsContents()
	{
		// The limb-rule half is the ITEM's own data (`usableOnLimb`), not the liquid
		// alone: a water bottle holding an injectable liquid still has no limb action,
		// so the injection guard does not claim it and its own use action (a drink)
		// does. This is the shape the deleted id tables answered by absence.
		Assert.Equal(ItemUseFamily.Drink, Classify(ItemUseEntry.Inventory, "waterbottle", ("fentanyl", 10f)));
		Assert.Equal(ItemUseFamily.Drink, Classify(ItemUseEntry.Limb, "waterbottle", ("fentanyl", 10f)));
	}

	[Fact]
	public void ALimbTool_IsAbsentFromTheInventoryEntry()
	{
		// A limb tool has no `useAction` at all — only a `useLimbAction` — so the
		// inventory use cannot mean it; only the wound view's release carries it.
		Assert.Equal(ItemUseFamily.None, Classify(ItemUseEntry.Inventory, "splint"));
		Assert.Equal(ItemUseFamily.LimbTool, Classify(ItemUseEntry.Limb, "splint"));
	}

	[Fact]
	public void AFoodThatAlsoCarriesALimbAction_EatsOnTheWoundViewToo()
	{
		// bulbskin and xalorissponge both feed a body and carry a limb action. The
		// classifier asks the solid-food rule BEFORE the limb-tool rule on the limb
		// entry, which is what keeps one gesture from being routed twice (the family
		// order the host dispatches in). In production the medical gate refuses these
		// items outright (`IsMedicalLimbUseItem` does not admit food), so only a
		// hand-made request reaches this arm; its limb half stays unreachable until the
		// request can say which action was meant (LimbToolAdmission's own recorded gap).
		Assert.Equal(ItemUseFamily.SolidFood, Classify(ItemUseEntry.Inventory, "bulbskin"));
		Assert.Equal(ItemUseFamily.SolidFood, Classify(ItemUseEntry.Limb, "bulbskin"));
	}

	[Fact]
	public void TheAutoSelect_SkipsAnInjectableContainerTheHostWouldRefuse()
	{
		// The injection family is a named REFUSAL, not a family this path carries: an
		// auto-selected injectable container would make the whole request fail instead
		// of offering the usable item behind it (the old OR-of-rules tree never offered
		// it either). The drink/meal behind it is what auto-select picks.
		var injectable = new CharacterItemMsg
		{
			InstanceId = 41,
			ItemId = "saline",
			SlotIndex = 0,
			Condition = 1f,
			Liquids = [new LiquidStackMsg { LiquidId = "saline", Amount = 750f }],
		};
		var bread = new CharacterItemMsg
		{
			InstanceId = 42,
			ItemId = "bread",
			SlotIndex = 1,
			Condition = 0.75f,
		};

		var picked = CarriedItemUseTree.FindFirstUsable(
			[injectable, bread],
			ItemUseEntry.Inventory,
			FakeLimbUseSemantics.Instance,
			FakeConsumeSemantics.Instance,
			FakeWearSemantics.Instance,
			FakeSolidFoodSemantics.Instance);

		Assert.NotNull(picked);
		Assert.Equal(42UL, picked!.InstanceId);
	}

	[Fact]
	public void AWearable_IsTheWearPlacementOnBothEntries()
	{
		// A wearable is a wearable whatever else the gesture could be, and it is asked
		// before every family on both entries, exactly as the host's chain dispatches.
		Assert.Equal(ItemUseFamily.Wear, Classify(ItemUseEntry.Inventory, "bikehelmet"));
		Assert.Equal(ItemUseFamily.Wear, Classify(ItemUseEntry.Limb, "bikehelmet"));
	}

	[Fact]
	public void AFoodThatHandsTheEaterAReplacementObject_IsTheSolidFoodFamily()
	{
		// The replacement shape is carried: the object the action leaves behind reaches the item's OWNER, never the
		// eater's world (the hand-feed arrangement, user ruling 2026-10-25), so the classifier offers it as the solid
		// food it is rather than as no family at all.
		Assert.Equal(ItemUseFamily.SolidFood, Classify(ItemUseEntry.Inventory, "bucketofchicken"));
		Assert.Equal(ItemUseFamily.SolidFood, Classify(ItemUseEntry.Limb, "bucketofchicken"));
	}

	[Fact]
	public void AnItemNoFamilyClaims_IsNoneOnBothEntries()
	{
		Assert.Equal(ItemUseFamily.None, Classify(ItemUseEntry.Inventory, "knife"));
		Assert.Equal(ItemUseFamily.None, Classify(ItemUseEntry.Limb, "knife"));
		Assert.Equal(ItemUseFamily.None, Classify(ItemUseEntry.Inventory, ""));
		Assert.Equal(ItemUseFamily.None, Classify(ItemUseEntry.Limb, ""));
	}
}
