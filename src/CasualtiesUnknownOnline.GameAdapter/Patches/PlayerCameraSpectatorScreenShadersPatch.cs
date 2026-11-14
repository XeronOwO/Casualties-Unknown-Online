using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The watched body's pain, blood, oxygen and damage shaders are written by <c>PlayerCamera.HandleScreenShaders</c>,
/// which reads <c>this.body</c> throughout (PlayerCamera.cs:3255-3339; the pain value at 3258/3293). The method
/// has no body parameter and no other redirect point, so the spectator's presentation is obtained by pointing the
/// camera at the watched body for the duration of the call and restoring it immediately afterwards. The local
/// body is never mutated by the call: the fields written are the camera's screen material and its smoothing state.
/// </summary>
[HarmonyPatch(typeof(PlayerCamera), "HandleScreenShaders")]
internal static class PlayerCameraSpectatorScreenShadersPatch
{
	private static void Prefix(PlayerCamera __instance, out Body __state)
	{
		__state = __instance.body;
		if (!SpectatorPresentationGate.IsActive || SpectatorPresentationGate.HudBody is not { } watched)
		{
			return;
		}

		__instance.body = watched;
	}

	private static void Postfix(PlayerCamera __instance, Body __state)
	{
		if (__state != null) // Unity object — ==
		{
			__instance.body = __state;
		}
	}
}
