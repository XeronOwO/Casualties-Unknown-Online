using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// A player drank (FluidManager.DrinkLiquid — the full local effect ran: the
/// body effects landed and the local grid cell cleared). The consumption is
/// reported so the host clears its OWN grid (the authority) and relays; the
/// body effects ride the CharacterData report. Only the LOCAL player's drink
/// is reported — a clone never drinks.
/// </summary>
[HarmonyPatch(typeof(FluidManager), "DrinkLiquid")]
internal static class FluidDrinkPatch
{
	private static void Postfix(FluidManager __instance, Vector2Int pos, Body body)
	{
		if (PatchBridge.Impl is not { } bridge || !bridge.IsSessionActive)
		{
			return;
		}

		if (PlayerCamera.main == null || body != PlayerCamera.main.body) // Unity objects — ==
		{
			return;
		}

		bridge.OnFluidDrinkReported(pos);
	}
}
