using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

/// <summary>
/// The ONE answer to "which family does this gesture run", asked by the one-shot
/// routing sites — the host's dispatch, the world-drag gate, the auto-select and the
/// medical view's dose measurement — so the two ends of a request cannot disagree
/// about what the gesture is. (The remote medical view's own dispatch keeps its
/// wider gate for the chains that have not migrated; this classifier is what those
/// sites agree on for the one-shot families.)
/// <para>
/// The ENTRY decides which rules are asked at all (decision 246): the inventory use
/// never asks a limb rule, because its native call site (<c>Body.UseItem</c> →
/// <c>ItemInfo.useAction</c>) has no limb to carry, while the wound view's release
/// (<c>PlayerCamera.ApplyWoundItem</c> → <c>useLimbAction</c> /
/// <c>WaterContainerItem.ApplyToLimb</c>) asks them. Within an entry the order is
/// the one the host's chain dispatches in: the injection refusal first, because a
/// usable liquid container can also be injectable (a blood bag's own use action
/// draws blood) and the drink measurement must not run that action. That refusal is
/// keyed on the STACKS; a usable container whose stacks carry no injectable flag is
/// a drink by its own data, and the drink measurement classifies the DEFINITION
/// before running it, so a non-drink action is refused unrun
/// (`done/drink-measurement-destructive-use-action.md`).
/// </para>
/// <para>
/// An item whose data satisfies one entry's family and the other entry's is not a
/// conflict to resolve: it is two actions, each reached through its own gesture.
/// The container this rule was extracted for is a drinkable bag (its own
/// <c>useAction</c> drinks, <c>Item.cs:1867-1870</c>) holding a health-usable liquid
/// (so the limb release may apply it topically); the world drag drinks from it and
/// the wound view applies its health-usable half.
/// </para>
/// </summary>
public static class ItemUseFamilyClassifier
{
	/// <summary>
	/// The family this item's gesture belongs to on the given entry. The measurement
	/// sites and the host's dispatch ask THIS rather than one family's predicate on
	/// its own, so a family the host will refuse is never measured and a family the
	/// entry does not carry is never dispatched.
	/// </summary>
	public static ItemUseFamily Classify(
		ItemUseEntry entry,
		string itemId,
		IReadOnlyList<LiquidStackMsg>? liquids,
		ILimbUseSemantics limbSemantics,
		IConsumeSemantics consumeSemantics,
		IWearSemantics wearSemantics,
		ISolidFoodSemantics solidFoodSemantics) =>
		entry == ItemUseEntry.Limb
			? ClassifyLimb(itemId, liquids, limbSemantics, consumeSemantics, wearSemantics, solidFoodSemantics)
			: ClassifyInventory(itemId, liquids, limbSemantics, consumeSemantics, wearSemantics, solidFoodSemantics);

	/// <summary>
	/// The inventory use's families and nothing else: native <c>Body.UseItem</c> runs
	/// the item's own <c>useAction</c>, so the rules asked here are the wear placement,
	/// the injection refusal (admitted only so the host can refuse it BY NAME before
	/// an injectable container's destructive action is measured), the drink and the
	/// eat. The topical and limb-tool rules are absent ON PURPOSE — their native call
	/// site is the wound view's release, whose limb this gesture cannot carry
	/// (decision 246) — so the item's own data can never route a world drag through a
	/// limb action.
	/// </summary>
	private static ItemUseFamily ClassifyInventory(
		string itemId,
		IReadOnlyList<LiquidStackMsg>? liquids,
		ILimbUseSemantics limbSemantics,
		IConsumeSemantics consumeSemantics,
		IWearSemantics wearSemantics,
		ISolidFoodSemantics solidFoodSemantics) =>
		WearAdmission.IsWearable(wearSemantics, itemId)
			? ItemUseFamily.Wear
			: InjectionAdmission.IsInjectableContainer(limbSemantics, itemId, liquids)
				? ItemUseFamily.Injection
				: ConsumeAdmission.IsDrinkContainer(consumeSemantics, itemId, liquids)
					? ItemUseFamily.Drink
					: SolidFoodAdmission.IsFeedable(solidFoodSemantics, itemId)
						? ItemUseFamily.SolidFood
						: ItemUseFamily.None;

	/// <summary>
	/// The wound view's release and its families: the same wear placement (the
	/// held-remote-item release reaches this entry with any proxy, and a wearable is
	/// still the host's wear branch), the injection refusal, then the topical
	/// application, the drink (a container with no health-usable stack still drinks
	/// when the held-remote release runs its use action against the acting body), the
	/// eat, and the limb tool last. The topical rule is asked before the drink rule
	/// because the container both admit is the one whose native limb action a limb
	/// release runs; the drink rule stays for the container the topical rule declines.
	/// </summary>
	private static ItemUseFamily ClassifyLimb(
		string itemId,
		IReadOnlyList<LiquidStackMsg>? liquids,
		ILimbUseSemantics limbSemantics,
		IConsumeSemantics consumeSemantics,
		IWearSemantics wearSemantics,
		ISolidFoodSemantics solidFoodSemantics) =>
		WearAdmission.IsWearable(wearSemantics, itemId)
			? ItemUseFamily.Wear
			: InjectionAdmission.IsInjectableContainer(limbSemantics, itemId, liquids)
				|| InjectionAdmission.IsInjectionShapedLimbContainer(limbSemantics, itemId)
				? ItemUseFamily.Injection
				: TopicalAdmission.IsTopicalContainer(limbSemantics, itemId, liquids)
					? ItemUseFamily.Topical
					// The drink rule models the item's use ACTION, which the limb release only runs for a container
					// with no limb action of its own: a `usableOnLimb` container's release runs `useLimbAction`
					// instead (PlayerCamera.ApplyWoundItem), so calling it a drink here would dispatch a family the
					// native entry never runs.
					: !limbSemantics.IsLimbUsableLiquidContainer(itemId)
						&& ConsumeAdmission.IsDrinkContainer(consumeSemantics, itemId, liquids)
						? ItemUseFamily.Drink
						: SolidFoodAdmission.IsFeedable(solidFoodSemantics, itemId)
							? ItemUseFamily.SolidFood
							: LimbToolAdmission.IsLimbTool(limbSemantics, solidFoodSemantics, itemId)
								? ItemUseFamily.LimbTool
								: ItemUseFamily.None;
}
