using UnityEngine;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Grabber plant → EnemyEffectMsg.GrabberGrabbed (repeatable — every 5 s):
/// the tendrils grabbed the LOCAL body (GrabberPlant.cs:75-90 — the grab's
/// ragdoll/scream ride the entity-state stream and the speech domain; the
/// tendril animation is Update-driven on every side). The verified no-grab →
/// grab transition reports the post-grab shock/eye-panic terminal state as the
/// dedicated enemy-effect event, never the 1 Hz snapshot.
/// </summary>
[HarmonyPatch(typeof(GrabberPlant), "Update")]
internal static class TrapGrabberPlantPatch
{
	private static void Prefix(GrabberPlant __instance, out bool __state) =>
		__state = Traverse.Create(__instance).Field("grabBody").GetValue<Rigidbody2D>() != null;

	private static void Postfix(GrabberPlant __instance, bool __state)
	{
		if (__state || Traverse.Create(__instance).Field("grabBody").GetValue<Rigidbody2D>() == null)
		{
			return; // not the no-grab → grab transition
		}

		var playerCamera = PlayerCamera.main;
		var body = playerCamera != null ? playerCamera.body : null;
		if (body != null) // Unity object — ==
		{
			PatchBridge.Impl?.OnGrabberGrabbed(body);
		}
	}
}
