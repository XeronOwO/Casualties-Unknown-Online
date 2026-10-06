using System;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// While the remote backpack view is open, <c>PlayerCamera.UpdateWearables</c>
/// builds the worn-item drop buttons from the focused remote clone's body
/// instead of the local body. The original body is restored in the postfix, so
/// the switch is scoped to the native wear-button construction call only, and
/// again in the finalizer: a postfix does not run for a throwing body, and this
/// call happens INSIDE every release bracket
/// (<c>PlayerCamera.HandleReleaseDragging</c> calls it before it clears the
/// drag), so a throw would leave <c>camera.body</c> on a display clone for the
/// rest of the session — which also silently disarms the display-body redirect
/// the release seams are built on (<c>RemoteDragPredicateView.AnsweringBody</c>
/// answers only while <c>camera.body</c> is the local body). The finalizer
/// restores the field and lets the same exception propagate: it hardens state,
/// it never swallows a failure.
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

	private static Exception? Finalizer(PlayerCamera __instance, Exception? __exception, ref Body __state)
	{
		if (__exception is not null)
		{
			// The postfix above belongs to the normal path only.
			__instance.body = __state;
		}

		return __exception;
	}
}
