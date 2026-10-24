using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The spectator's screen effects are off. <c>PlayerCamera.HandleUnconsciousScreen</c> is the one writer of the
/// local body's screen presentation — the black amount, the consciousness text and vignette, the dying/death fade —
/// and a spectator watches, so it does not run while the mode is on. The prefix also CLEARS what a previous state
/// left behind (the receipt's black screen is at full black when the mode is entered from it), because suppressing
/// the writer alone would leave the spectator looking at a black frame forever; on exit the game's own code
/// recomputes everything from the body.
/// </summary>
[HarmonyPatch(typeof(PlayerCamera), "HandleUnconsciousScreen")]
internal static class PlayerCameraSpectatorEffectsPatch
{
	private static bool Prefix(PlayerCamera __instance)
	{
		if (!SpectatorPresentationGate.IsActive)
		{
			return true;
		}

		__instance.blackAmount = 0f;
		if (__instance.consciousnessVignette != null) // Unity object — ==
		{
			__instance.consciousnessVignette.color = Color.clear;
		}

		return false;
	}
}
