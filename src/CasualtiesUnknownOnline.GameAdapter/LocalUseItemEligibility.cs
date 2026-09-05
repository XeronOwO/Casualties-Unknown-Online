using System;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Pure stateless eligibility for the KrokMP-style drag-to-use entry: a dragged
/// inventory item can be released on another player only when its id and liquid
/// stack are in one of the supported remote-item catalogs. Split out of
/// <see cref="PlayerInteractionApply"/> at the 600-line gate; no game state.
/// </summary>
internal static class LocalUseItemEligibility
{
	public static bool IsUseItem(Item item)
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

		if (RemoteMedicineCatalog.IsInjectableItem(item.id))
		{
			var medicine = item.GetComponent<WaterContainerItem>();
			if (medicine == null || medicine.CurrentTotal <= 0f) // Unity object — ==
			{
				return false;
			}

			foreach (var liquid in medicine.stack)
			{
				if (!RemoteMedicineCatalog.IsSupportedMedicineLiquid(liquid.liquidId))
				{
					return false;
				}
			}

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

		if (RemoteTopicalCatalog.IsTopicalItem(item.id))
		{
			var topical = item.GetComponent<WaterContainerItem>();
			if (topical == null || topical.CurrentTotal <= 0f) // Unity object — ==
			{
				return false;
			}

			foreach (var liquid in topical.stack)
			{
				if (!RemoteTopicalCatalog.IsSupportedTopicalLiquid(liquid.liquidId))
				{
					return false;
				}
			}

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
	public static bool IsMedicalLimbUseItem(Item item)
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

		if (RemoteMedicineCatalog.IsInjectableItem(item.id))
		{
			return HasValidLiquid(item, RemoteMedicineCatalog.IsSupportedMedicineLiquid);
		}

		if (RemoteTopicalCatalog.IsTopicalItem(item.id))
		{
			return HasValidLiquid(item, RemoteTopicalCatalog.IsSupportedTopicalLiquid);
		}

		return false;
	}

	private static bool HasValidLiquid(Item item, Func<string, bool> isSupportedLiquid)
	{
		var container = item.GetComponent<WaterContainerItem>();
		if (container == null || container.CurrentTotal <= 0f) // Unity object — ==
		{
			return false;
		}

		foreach (var liquid in container.stack)
		{
			if (!isSupportedLiquid(liquid.liquidId))
			{
				return false;
			}
		}

		return true;
	}
}
