using CasualtiesUnknownOnline.Runtime.Protocol;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Turret → TurretFired (visual, repeatable — the 15 s reload is the game's
/// own gate) and → TurretSelfDestructed (one-shot, explosion family): the
/// health &lt; 350 countdown finished and the turret exploded itself
/// (TurretScript.cs:85-103 — explodeCount flips back to 0 as it fires the
/// custom-parameter explosion). Pure observation both ways; the event replays
/// the warning/shot visuals and the self-destruct on the peers.
///
/// TurretFired reports at the WARNING moment — didBeep flipped, the event's
/// TRUE START (user mandate 2026-08-10: report at the real event moment): the
/// trigger side beeped (turretsee, TurretScript.cs:69-73) and its shot follows
/// 0.5 s later (the beepTime countdown, :40-53); the replay re-runs that chain
/// from the same start (warning now, rifleshot/particles/tracer in 0.5 s —
/// TrapStateActions.ApplyTurretFired), so both sides warn and fire TOGETHER.
/// Reporting at Shoot() instead (the pre-fix hook, t = 0.5 s) made the whole
/// replay chain half a second late — the observed lag. The shot itself is NOT
/// reported: it is the beep's guaranteed follow-up (beepTime &gt;= 0.5 fires
/// unconditionally) and the replay's 0.5 s coroutine lands on the same moment.
/// </summary>
internal static class TrapTurretPatch
{
	[HarmonyPatch(typeof(TurretScript), "Update")]
	internal static class WarningPatch
	{
		private static void Prefix(TurretScript __instance, out bool __state) =>
			__state = Traverse.Create(__instance).Field("didBeep").GetValue<bool>();

		private static void Postfix(TurretScript __instance, bool __state)
		{
			var now = Traverse.Create(__instance).Field("didBeep").GetValue<bool>();
			if (!now || __state)
			{
				return; // not the warning frame (didBeep just flipped)
			}

			PatchBridge.Impl?.OnTrapTriggered(EntityEventKind.TurretFired, __instance.transform.position, 0);
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
