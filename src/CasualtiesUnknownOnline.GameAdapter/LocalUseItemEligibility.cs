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
/// </summary>
internal static class LocalUseItemEligibility
{
	public static bool IsUseItem(Item item, ILimbUseSemantics semantics)
	{
		if (item == null || item.condition <= 0f) // Unity object — ==
		{
			return false;
		}

		if (RemoteWearCatalog.IsWearItem(item.id))
		{
			return true;
		}

		if (RemoteConsumeCatalog.IsFoodItem(item.id))
		{
			return true;
		}

		if (IsInjectableRemoteItem(item, semantics))
		{
			return true;
		}

		if (RemoteDrinkMedicineCatalog.IsDrinkableMedicineItem(item.id))
		{
			var drinkMedicine = item.GetComponent<WaterContainerItem>();
			if (drinkMedicine == null || drinkMedicine.CurrentTotal <= 0f) // Unity object — ==
			{
				return false;
			}

			foreach (var liquid in drinkMedicine.stack)
			{
				if (!RemoteDrinkMedicineCatalog.IsSupportedDrinkMedicineLiquid(liquid.liquidId))
				{
					return false;
				}
			}

			return true;
		}

		if (IsTopicalRemoteItem(item, semantics))
		{
			return true;
		}

		if (RemoteLimbToolCatalog.IsToolItem(item.id))
		{
			return true;
		}

		var water = item.GetComponent<WaterContainerItem>();
		if (water == null || water.CurrentTotal <= 0f) // Unity object — ==
		{
			return false;
		}

		foreach (var liquid in water.stack)
		{
			if (!RemoteConsumeCatalog.IsKnownLiquid(liquid.liquidId))
			{
				return false;
			}
		}

		return true;
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
		HasLimbContainer(item, out var container)
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
		HasLimbContainer(item, out var container)
		&& TopicalAdmission.IsTopicalContainer(semantics, item.id, ToLiquidStacks(container));

	/// <summary>An item that can still be drawn from: alive and holding liquid.</summary>
	private static bool HasLimbContainer(Item item, out WaterContainerItem container)
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
