using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// PlayerCamera.DoAlert deferral during the start-gate wait. The game builds
/// the layer-title popup immediately after hiding the loading screen
/// (WorldGeneration.cs:3637/3640-3659), which is one frame BEFORE the host's
/// world-entry edge arms the gate — so the popup's 6 s unscaled lifetime
/// (PlayerCamera.cs:3050-3058) used to play out invisibly while the gate held.
/// The prefix queues the popup while the alert window is open and returns
/// false to skip the original; StartGateCoordinator replays the queue in order
/// once the run is playing.
/// </summary>
[HarmonyPatch(typeof(PlayerCamera), nameof(PlayerCamera.DoAlert))]
internal static class PlayerCameraDoAlertPatch
{
	private static bool Prefix(string text, bool important) =>
		PatchBridge.Impl?.TryDeferStartGateAlert(text, important) != true;
}
