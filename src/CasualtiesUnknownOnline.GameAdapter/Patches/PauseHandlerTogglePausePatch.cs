using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Start-gate pause cooperation. The gate freezes the world with
/// Time.timeScale = 0, but the game's own PauseHandler fights that:
/// its Update force-restores Normal speed whenever timeScale is 0 without a
/// pause UI (PauseHandler.cs:157-160), and TogglePause would open the pause
/// menu under our overlay (and let the player quit). While the gate holds,
/// both are skipped; the release restores the normal flow.
/// </summary>
[HarmonyPatch(typeof(PauseHandler), "TogglePause")]
internal static class PauseHandlerTogglePausePatch
{
	private static bool Prefix()
	{
		var bridge = PatchBridge.Impl;
		return bridge is null || (!bridge.IsWaitingForReady && !bridge.IsOnlineUiModalOpen);
	}
}
