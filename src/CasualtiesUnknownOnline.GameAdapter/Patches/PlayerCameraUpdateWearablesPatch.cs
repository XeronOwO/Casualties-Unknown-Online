using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// While the remote backpack view is open, <c>PlayerCamera.UpdateWearables</c>
/// builds the worn-item drop buttons from the focused remote clone's body
/// instead of the local body. The original body is restored in the postfix, so
/// the switch is scoped to the native wear-button construction call only.
/// </summary>
[HarmonyPatch(typeof(PlayerCamera), "UpdateWearables")]
internal static class PlayerCameraUpdateWearablesPatch
{
	private static void Prefix(PlayerCamera __instance, ref Body __state)
	{
		__state = __instance.body;
		if (RemoteBackpackView.FocusedBody is { } focused) // Unity object destroyed is already filtered by getter
		{
			__instance.body = focused;
		}
	}

	private static void Postfix(PlayerCamera __instance, ref Body __state) =>
		__instance.body = __state;
}
