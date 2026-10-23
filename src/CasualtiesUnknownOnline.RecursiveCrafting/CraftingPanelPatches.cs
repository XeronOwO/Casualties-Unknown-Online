using CasualtiesUnknownOnline.RecursiveCrafting.Core.Crafting;
using HarmonyLib;

namespace CasualtiesUnknownOnline.RecursiveCrafting;

/// <summary>
/// The native recipe panel's own two surfaces, extended in place — the row's name and its hover tooltip. The
/// panel itself keeps deciding its two native states and its own layout; this only appends the third state's
/// marker (a non-empty value only for an entry the resolver found reachable through other crafts), so nothing is
/// duplicated and no row is re-created.
/// </summary>
internal static class CraftingPanelPatches
{
	/// <summary>The row text: <c>PlayerCamera.RefreshRecipeList</c> writes <c>recipe.fullName</c> into it.</summary>
	[HarmonyPatch(typeof(Recipe), "get_fullName")]
	internal static class RecipeFullNamePatch
	{
		private static void Postfix(Recipe __instance, ref string __result)
		{
			if (!string.IsNullOrEmpty(__result) && CraftingPanelState.RecursiveState(__instance) is not null)
			{
				__result += CraftingPanelState.Marker;
			}
		}
	}

	/// <summary>The tooltip body: the native tooltip shows <c>recipe.description</c>, so the steps land there.</summary>
	[HarmonyPatch(typeof(Recipe), "get_description")]
	internal static class RecipeDescriptionPatch
	{
		private static void Postfix(Recipe __instance, ref string __result)
		{
			if (CraftingPanelState.RecursiveState(__instance) is not { } resolution)
			{
				return;
			}

			var steps = CraftChainReport.DescribeSteps(resolution);
			if (steps.Length == 0)
			{
				return;
			}

			__result = (__result ?? string.Empty) + "\n\n" + CraftingPanelState.TooltipLead + steps;
		}
	}
}
