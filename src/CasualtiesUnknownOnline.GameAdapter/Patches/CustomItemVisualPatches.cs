using CasualtiesUnknownOnline.GameAdapter.Content;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Local/remote visual presentation patches for custom item worn sprites and
/// liquid masks. <see cref="CustomItemVisualState"/> is authored on the cached
/// runtime template; these thin hooks apply the worn sprite at the moment an
/// item actually lands on a limb, restore the normal sprite when it comes off,
/// and re-apply the water-container liquid mask after the native Start path.
/// </summary>
internal static class CustomItemVisualPatches
{
	[HarmonyPatch(typeof(Body), "WearWearable")]
	internal static class WearWearableVisualPatch
	{
		private static void Postfix(Item item)
		{
			if (item == null
				|| item.transform.parent == null
				|| item.transform.parent.GetComponent<Limb>() == null) // Unity objects — ==
			{
				return;
			}

			item.GetComponent<CustomItemVisualState>()?.ApplyWornVisual();
		}
	}

	[HarmonyPatch(typeof(Body), "DropWearable")]
	internal static class DropWearableVisualPatch
	{
		private static void Postfix(Item item)
		{
			if (item == null) // Unity object — ==
			{
				return;
			}

			item.GetComponent<CustomItemVisualState>()?.RestoreNormalVisual();
		}
	}

	[HarmonyPatch(typeof(WaterContainerItem), "Start")]
	internal static class LiquidMaskStartPatch
	{
		private static void Postfix(WaterContainerItem __instance)
		{
			if (__instance == null) // Unity object — ==
			{
				return;
			}

			__instance.GetComponent<CustomItemVisualState>()?.ApplyLiquidMask();
		}
	}

	[HarmonyPatch(typeof(Wearable), "CreateSprites")]
	internal static class CreateSpritesMultiWornPatch
	{
		private static void Prefix(Wearable __instance, Body body)
		{
			if (__instance == null) // Unity object — ==
			{
				return;
			}

			var item = __instance.GetComponent<Item>();
			if (item == null) // Unity object — ==
			{
				return;
			}

			item.GetComponent<CustomItemVisualState>()?.ConfigureWearableSecondarySprites(__instance, body);
		}

		private static void Postfix(Wearable __instance)
		{
			if (__instance == null) // Unity object — ==
			{
				return;
			}

			var item = __instance.GetComponent<Item>();
			if (item == null) // Unity object — ==
			{
				return;
			}

			item.GetComponent<CustomItemVisualState>()?.ApplySecondarySpritePresentation(__instance);
		}
	}
}
