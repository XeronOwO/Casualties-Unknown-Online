using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Pure stateless eligibility projection for the local use-item picker: an
/// inventory item is exposed to the Online UI's Use button only when its id and
/// liquid stack are in one of the supported remote-item catalogs. Split out of
/// <see cref="PlayerInteractionApply"/> at the 600-line gate; no game state.
/// </summary>
internal static class LocalUseItemEligibility
{
	public static bool HasUseItemChild(Transform parent)
	{
		for (var c = 0; c < parent.childCount; c++)
		{
			var item = parent.GetChild(c).GetComponent<Item>();
			if (item != null && IsUseItem(item)) // Unity object — ==
			{
				return true;
			}
		}

		return false;
	}

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
}
