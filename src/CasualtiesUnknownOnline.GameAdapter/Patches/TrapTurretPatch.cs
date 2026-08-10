using CasualtiesUnknownOnline.Runtime.Protocol;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Turret → TurretFired (visual, repeatable — the 15 s cooldown is the game's
/// own gate) and → TurretSelfDestructed (one-shot, explosion family): the
/// health &lt; 350 countdown finished and the turret exploded itself
/// (TurretScript.cs:85-103 — explodeCount flips back to 0 as it fires the
/// custom-parameter explosion). Pure observation both ways; the event replays
/// the tracers/shot and the self-destruct on the peers.
/// </summary>
internal static class TrapTurretPatch
{
	[HarmonyPatch(typeof(TurretScript), "Shoot")]
	internal static class ShootPatch
	{
		private static void Postfix(FireInfo info)
		{
			// Static method — no __instance. The FireInfo's ignoreTrans IS the
			// turret (TurretScript.cs:47 passes base.transform), its position is
			// the entity key.
			var turret = info.ignoreTrans != null ? info.ignoreTrans.GetComponent<TurretScript>() : null; // Unity object — ==
			if (turret == null)
			{
				return;
			}

			PatchBridge.Impl?.OnTrapTriggered(EntityEventKind.TurretFired, turret.transform.position, 0);
		}
	}

	[HarmonyPatch(typeof(TurretScript), "Update")]
	internal static class SelfDestructPatch
	{
		private static void Prefix(TurretScript __instance, out float __state) =>
			__state = Traverse.Create(__instance).Field("explodeCount").GetValue<float>();

		private static void Postfix(TurretScript __instance, float __state)
		{
			var now = Traverse.Create(__instance).Field("explodeCount").GetValue<float>();
			if (__state <= 0f || now != 0f)
			{
				return; // not the countdown-finished frame (explodeCount just reset)
			}

			PatchBridge.Impl?.OnTrapTriggered(EntityEventKind.TurretSelfDestructed, __instance.transform.position, 0);
		}
	}
}
