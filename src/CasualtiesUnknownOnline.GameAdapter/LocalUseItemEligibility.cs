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
/// The wear family is asked through the same <see cref="IWearSemantics"/> seam the
/// host's chain asks (<see cref="WearAdmission"/>), so "is this a wearable" has
/// one answer on both sides; it is deliberately NOT a
/// <see cref="Family"/> member, because a wear gesture has nothing to measure —
/// the operator sends the request and the host does the placement.
/// </para>
/// </summary>
internal static class LocalUseItemEligibility
{
	/// <summary>
	/// The families the one-shot path carries, in the order the host's chain asks
	/// them (<c>PlayerItemUseService</c>: wear, the injection refusal, topical,
	/// drink, food, limb tool). <see cref="FamilyOf"/> is the ONE answer to "which
	/// family is this gesture" on the operator's side, so a measurement site can
	/// never run an item's own native action for a family the host will refuse.
	/// </summary>
	internal enum Family
	{
		/// <summary>No family this path carries; the host refuses it by name and nothing may be measured here.</summary>
		None,

		/// <summary>The medical operation session's family: this path refuses it by name, and its own action must NOT run here — a blood bag's <c>useAction</c> draws blood.</summary>
		Injection,

		/// <summary>Measured through the item's own limb action (a limb application).</summary>
		Topical,

		/// <summary>Measured through the item's own use action (a drink).</summary>
		Drink,
	}

	/// <summary>
	/// The family this item's one-shot gesture belongs to, asked in the host
	/// chain's own order. The measurement sites ask THIS rather than one family's
	/// predicate on its own: the ordering is what keeps a container the game marks
	/// both ways on the family the host will dispatch, and what keeps a family the
	/// host REFUSES from being measured at all.
	/// </summary>
	internal static Family FamilyOf(Item item, ILimbUseSemantics limbSemantics, IConsumeSemantics consumeSemantics)
	{
		if (IsInjectableRemoteItem(item, limbSemantics))
		{
			return Family.Injection;
		}

		if (IsTopicalRemoteItem(item, limbSemantics))
		{
			return Family.Topical;
		}

		return IsDrinkRemoteItem(item, consumeSemantics) ? Family.Drink : Family.None;
	}

	/// <summary>
	/// The families in the order the host's one-shot chain asks them, so an item
	/// this gate admits is an item the host will either handle or refuse by name —
	/// never one it does not recognise. The wear rule comes first (the host's chain
	/// asks it before the injection refusal, because a wearable is a wearable
	/// whatever else its data says); the limb rules then come before the drink rule
	/// because the containers both admit (saline, ringersolution, a blood bag) are
	/// the medical family's, and because a mod container the game marks as a drink
	/// AND a topical carrier must measure the call the host will run.
	/// </summary>
	public static bool IsUseItem(Item item, ILimbUseSemantics limbSemantics, IConsumeSemantics consumeSemantics, IWearSemantics wearSemantics)
	{
		if (item == null || item.condition <= 0f) // Unity object — ==
		{
			return false;
		}

		if (WearAdmission.IsWearable(wearSemantics, item.id))
		{
			return true;
		}

		if (FamilyOf(item, limbSemantics, consumeSemantics) != Family.None)
		{
			return true;
		}

		if (RemoteConsumeCatalog.IsFoodItem(item.id))
		{
			return true;
		}

		return RemoteLimbToolCatalog.IsToolItem(item.id);
	}

	/// <summary>
	/// The narrower eligibility for the native WoundView remote-limb treatment
	/// gesture: only medical/limb-treatment surfaces may be dragged onto a
	/// remote body image. Wearable, food, and drink-only items are deliberately
	/// excluded because the native WoundView limb target is not their entry
	/// point, and they would otherwise be routed as a remote wear/feed action
	/// from the wrong UI.
	/// </summary>
	public static bool IsMedicalLimbUseItem(Item item, ILimbUseSemantics semantics)
	{
		if (item == null || item.condition <= 0f) // Unity object — ==
		{
			return false;
		}

		if (RemoteHealProfiles.IsHealItem(item.id))
		{
			return true;
		}

		if (RemoteLimbToolCatalog.IsToolItem(item.id))
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
