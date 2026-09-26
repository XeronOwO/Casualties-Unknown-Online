using System;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// World-liquid drink sound capture. Both clips of that family play inside
/// <c>FluidManager.DrinkLiquid</c> (FluidManager.cs:286): its own water
/// branch's <c>"drink"</c> (:314) and the liquid registry's <c>onDrink</c>
/// delegate, which is where the groundwater / lumalgae / oil / sap branches
/// land (:293-308 → Liquids.cs:1501). The scope opens only for the local
/// player's own body drinking in a plain local action; the container-drink half
/// of "drinking" rides the item-use scope instead, which is why the two halves
/// report different kinds.
/// </summary>
internal static class WorldDrinkSoundPatches
{
	[HarmonyPatch(typeof(FluidManager), "DrinkLiquid")]
	internal static class DrinkLiquidSoundPatch
	{
		private static void Prefix(Body body, out IDisposable? __state) =>
			__state = CaptureScopeGuard.IsLocalAction() && CaptureScopeGuard.IsLocalPlayerBody(body)
				? CallContext.Enter(CallContext.Origin.CharacterWorldDrink)
				: null;

		private static void Postfix(IDisposable? __state) => __state?.Dispose();
	}
}
