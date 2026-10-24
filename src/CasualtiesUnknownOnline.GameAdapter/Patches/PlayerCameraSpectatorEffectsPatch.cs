using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The spectator's screen effects are the WATCHED body's, never the local one. <c>PlayerCamera.HandleUnconsciousScreen</c>
/// is the one writer of the screen presentation — the black amount, the consciousness text and vignette, the
/// dying/death fade — and while the mode is on it must show what the followed player is living through instead of
/// what the local body (which is standing somewhere else) would produce. The prefix therefore writes the gate's
/// mirrored values: the watched body's black amount and vignette colour, or zeros when nobody is followed, which is
/// also what clears the receipt's full-black frame on entry. On exit the game's own code recomputes everything from
/// the local body.
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

		__instance.blackAmount = SpectatorPresentationGate.WatchedBlackAmount;
		if (__instance.consciousnessVignette != null) // Unity object — ==
		{
			__instance.consciousnessVignette.color = SpectatorPresentationGate.WatchedVignette;
		}

		return false;
	}
}
