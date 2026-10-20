using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Pure stateless eligibility for the KrokMP-style drag-to-use entry: a dragged
/// inventory item can be released on another player only when the game's own
/// data (or the chain that has not migrated yet) makes it a supported remote
/// item. Split out of <see cref="PlayerInteractionApply"/> at the 600-line gate;
/// no game state beyond the item it is handed.
/// <para>
/// The FAMILY of a gesture is not this file's judgement: it is
/// <see cref="ItemUseFamilyClassifier"/>'s, asked with the entry the gesture was
/// made through, so the operator's verdict, the host's dispatch and the medical
/// view's dose measurement are one answer. This file only maps the live
/// <see cref="Item"/> onto the classifier's inputs (its id, its live liquid
/// stacks) and keeps the two narrow questions the medical view asks on its own
/// (<see cref="IsMedicalLimbUseItem"/>, the measurement handlers' predicates).
/// </para>
/// <para>
/// The wear family is asked through the same <see cref="IWearSemantics"/> seam the
/// host's chain asks (<see cref="WearAdmission"/>), so "is this a wearable" has
/// one answer on both sides; it is a classifier family rather than a family that
/// is measured, because a wear gesture has nothing to measure — the operator
/// sends the request and the host does the placement.
/// </para>
/// </summary>
internal static class LocalUseItemEligibility
{
	/// <summary>
	/// The family this item's one-shot gesture belongs to on the given ENTRY, asked
	/// through the ONE classifier. The measurement sites ask THIS rather than one
	/// family's predicate on its own: a family the host will refuse is never measured,
	/// and a family the entry does not carry is never admitted — which is what keeps
	/// a container the game marks both ways (a drinkable bag holding a health-usable
	/// liquid) on the action its gesture made.
	/// </summary>
	internal static ItemUseFamily FamilyOf(
		Item item,
		ItemUseEntry entry,
		ILimbUseSemantics limbSemantics,
		IConsumeSemantics consumeSemantics,
		IWearSemantics wearSemantics,
		ISolidFoodSemantics solidFoodSemantics)
	{
		if (item == null) // Unity object — ==
		{
			return ItemUseFamily.None;
		}

		return ItemUseFamilyClassifier.Classify(entry, item.id, LiquidsOf(item), limbSemantics, consumeSemantics, wearSemantics, solidFoodSemantics);
	}

	/// <summary>
	/// The world drag's question — the cross-player form of USING an item from the
	/// inventory — asked as the inventory entry's own families: wear, the injection
	/// refusal, drink and solid food, never a limb rule (decision 246). A limb tool
	/// and a topical carrier are deliberately absent: their native call site is the
	/// wound view's release, which carries the limb the operator picked, and
	/// <see cref="IsMedicalLimbUseItem"/> is their entry.
	/// <para>
	/// The injection family is admitted only so the host can refuse it BY NAME: its
	/// native use action draws blood into the operator's bag, which is the one thing
	/// this gesture must never run.
	/// </para>
	/// </summary>
	public static bool IsUseItem(Item item, ILimbUseSemantics limbSemantics, IConsumeSemantics consumeSemantics, IWearSemantics wearSemantics, ISolidFoodSemantics solidFoodSemantics)
	{
		if (item == null || item.condition <= 0f) // Unity object — ==
		{
			return false;
		}

		return FamilyOf(item, ItemUseEntry.Inventory, limbSemantics, consumeSemantics, wearSemantics, solidFoodSemantics) != ItemUseFamily.None;
	}

	/// <summary>
	/// The narrower eligibility for the native WoundView remote-limb treatment
	/// gesture: only medical/limb-treatment surfaces may be dragged onto a
	/// remote body image. Wearable, food, and drink-only items are deliberately
	/// excluded because the native WoundView limb target is not their entry
	/// point, and they would otherwise be routed as a remote wear/feed action
	/// from the wrong UI.
	/// </summary>
	public static bool IsMedicalLimbUseItem(Item item, ILimbUseSemantics semantics, ISolidFoodSemantics solidFoodSemantics)
	{
		if (item == null || item.condition <= 0f) // Unity object — ==
		{
			return false;
		}

		if (RemoteHealProfiles.IsHealItem(item.id))
		{
			return true;
		}

		if (LimbToolAdmission.IsLimbTool(semantics, solidFoodSemantics, item.id))
		{
			return true;
		}

		if (RemoteBandageMinigameCatalog.IsBandageItem(item.id))
		{
			return true;
		}

		if (RemoteOtherMedicalCatalog.IsDefibrillator(item.id))
		{
			return true;
		}

		if (RemoteOtherMedicalCatalog.IsAmputationTool(item.id))
		{
			return true;
		}

		if (RemoteOtherMedicalCatalog.IsDislocationWrench(item.id))
		{
			return true;
		}

		if (IsInjectableRemoteItem(item, semantics))
		{
			return true;
		}

		if (IsTopicalRemoteItem(item, semantics))
		{
			return true;
		}

		return false;
	}

	/// <summary>
	/// The injection family's eligibility, answered by the game's own registries
	/// through the SAME registered <paramref name="semantics"/> seam the host's
	/// start check uses, and by the one admission rule
	/// (<see cref="InjectionAdmission"/>): the item is a limb-drawable liquid
	/// container and at least one of its stacks is an injectable liquid. No id
	/// table is consulted, so vanilla content the old catalog never carried and
	/// mod content that declares its own injectable liquid both qualify.
	/// </summary>
	internal static bool IsInjectableRemoteItem(Item item, ILimbUseSemantics semantics) =>
		HasDrawableContainer(item, out var container)
		&& InjectionAdmission.IsInjectableContainer(semantics, item.id, ToLiquidStacks(container));

	/// <summary>
	/// The topical family's eligibility, the mirror of
	/// <see cref="IsInjectableRemoteItem"/> over the SAME seam: the item is a
	/// limb-drawable liquid container and at least one of its stacks is a
	/// health-usable liquid (<see cref="TopicalAdmission"/>), which is the flag
	/// <c>WaterContainerItem.ApplyToLimb</c> gates the liquid's own
	/// <c>onHealthUse</c> on. The injection check is asked first at every routing
	/// site, so a container carrying both kinds stays on the chain that applies a
	/// per-ml effect.
	/// </summary>
	internal static bool IsTopicalRemoteItem(Item item, ILimbUseSemantics semantics) =>
		HasDrawableContainer(item, out var container)
		&& TopicalAdmission.IsTopicalContainer(semantics, item.id, ToLiquidStacks(container));

	/// <summary>
	/// The drink family's eligibility, over the consume seam: the item is a
	/// liquid container the game may USE as one (<c>LiquidItemInfo.usable</c>,
	/// the flag <c>Body.UseItem</c> gates the item's own <c>useAction</c> on —
	/// <see cref="ConsumeAdmission"/>) and it still holds liquid to drink. The
	/// deleted catalogs answered this from an id list plus a per-liquid allowlist,
	/// so a liquid the game knows but the list missed was refused; now the item's
	/// own data and its live container answer it.
	/// </summary>
	internal static bool IsDrinkRemoteItem(Item item, IConsumeSemantics semantics) =>
		HasDrawableContainer(item, out var container)
		&& ConsumeAdmission.IsDrinkContainer(semantics, item.id, ToLiquidStacks(container));

	/// <summary>
	/// The item's live stacks in the shape the classifier reads, or null when it is
	/// not a container holding liquid to draw — the same gate the three predicate
	/// wrappers above use, so the classifier and the medical view's predicates read
	/// one item the same way.
	/// </summary>
	private static List<LiquidStackMsg>? LiquidsOf(Item item)
	{
		if (item.condition <= 0f || !HasDrawableContainer(item, out var container))
		{
			return null;
		}

		return ToLiquidStacks(container);
	}

	/// <summary>An item that can still be drawn from: alive and holding liquid.</summary>
	private static bool HasDrawableContainer(Item item, out WaterContainerItem container)
	{
		container = null!;
		if (item == null || item.condition <= 0f) // Unity object — ==
		{
			return false;
		}

		var found = item.GetComponent<WaterContainerItem>();
		if (found == null || found.CurrentTotal <= 0f) // Unity object — ==
		{
			return false;
		}

		container = found;
		return true;
	}

	/// <summary>The container's live stacks in the wire/kernel shape the admission rules read.</summary>
	private static List<LiquidStackMsg> ToLiquidStacks(WaterContainerItem container)
	{
		var liquids = new List<LiquidStackMsg>(container.stack.Count);
		foreach (var stack in container.stack)
		{
			liquids.Add(new LiquidStackMsg { LiquidId = stack.liquidId, Amount = stack.amount });
		}

		return liquids;
	}
}
