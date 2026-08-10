using CasualtiesUnknownOnline.Runtime.Protocol;
using UnityEngine;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Grabber plant → GrabberGrabbed (repeatable — every 5 s): the tendrils
/// grabbed a body (GrabberPlant.cs — the grab + scream are the LOCAL player's
/// interaction; the plant's own animation is Update-driven on every side).
/// The event is a trace point: there is nothing to replay — the grab's visuals
/// are the player-side ragdoll/scream (each side's own body) and the tendril
/// animation runs naturally everywhere.
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

		PatchBridge.Impl?.OnTrapTriggered(EntityEventKind.GrabberGrabbed, __instance.transform.position, 0);
	}
}
