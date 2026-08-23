using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// While the CUO Online UI modal window is open, the game's native
/// <c>PlayerCamera.HandleInput</c> must not process keyboard input — most
/// importantly the pause/ESC key would otherwise toggle the pause menu behind
/// the Online UI. The modal itself is closed by the UI layer on the same ESC
/// key in OnGUI (after this Update path has already been suppressed), so the
/// key never reaches the game both ways.
/// </summary>
[HarmonyPatch(typeof(PlayerCamera), "HandleInput")]
internal static class PlayerCameraHandleInputPatch
{
	private static bool Prefix() => PatchBridge.Impl is not { IsOnlineUiModalOpen: true };
}
