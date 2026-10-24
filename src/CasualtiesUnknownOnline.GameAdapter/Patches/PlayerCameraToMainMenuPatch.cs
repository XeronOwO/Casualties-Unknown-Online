using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The death screen's receipt button enters spectator mode instead of leaving the run. The receipt's button is
/// scene-wired to <c>PlayerCamera.ToMainMenu</c> (a <c>PreGen</c> scene load), and a scene's persistent listener
/// cannot be removed at runtime, so this prefix intercepts the METHOD while exactly that button is the live thing
/// on screen: a dead local body, the receipt active, and no pause menu open. In that state the port enters the
/// mode and the native load is skipped; every other caller — the pause menu's give-up button, the console's quit
/// verb, a live player — keeps the method's own meaning.
/// </summary>
[HarmonyPatch(typeof(PlayerCamera), "ToMainMenu")]
internal static class PlayerCameraToMainMenuPatch
{
	private static bool Prefix(PlayerCamera __instance)
	{
		var body = __instance.body;
		if (body == null || body.alive) // Unity object — ==
		{
			return true;
		}

		if (!__instance.didDeathScreen || __instance.endScreen == null || !__instance.endScreen.gameObject.activeSelf) // Unity object — ==
		{
			return true;
		}

		// The pause menu's own quit keeps working: its give-up button must still leave the run.
		if (PauseHandler.paused)
		{
			return true;
		}

		return PatchBridge.Spectator?.TryEnterFromDeathScreen() != true;
	}
}
