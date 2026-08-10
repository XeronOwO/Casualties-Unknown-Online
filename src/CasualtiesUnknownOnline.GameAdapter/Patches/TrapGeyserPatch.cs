using CasualtiesUnknownOnline.Runtime.Protocol;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Geyser → GeyserActivated (repeatable, the game's own 10 s cooldown is the
/// gate): the RUMBLE started — TryRumble passed its gate and set rumbleTime = 1
/// (GeyserScript.cs:19-27), the eruption's 1 s forewarning. The report rides
/// the event's TRUE START (user mandate 2026-08-10: report at the real event
/// moment): the receiving sides re-run TryRumble and rumbling together (sound +
/// shake), and their own Updates erupt together 1 s later — replaying the state
/// machine IS the sync, it is NOT a re-rumble. A DENIED TryRumble (cooldown /
/// already rumbling) leaves rumbleTime unchanged, so the transition check drops
/// it without a report. Reporting at Activate() instead (the pre-fix hook) made
/// the peers start rumbling only after the trigger side had already erupted —
/// the observed 1 s lag. The liquid type is NOT part of the event: it is bound
/// at generation time by the host (GeyserStateSnapshot, #128 — GeyserScript.Start
/// rolls it from the PUBLIC random stream, per-side, so the host's roll is the
/// authority).
/// </summary>
[HarmonyPatch(typeof(GeyserScript), "TryRumble")]
internal static class TrapGeyserPatch
{
	private static void Prefix(GeyserScript __instance, out float __state) =>
		__state = Traverse.Create(__instance).Field("rumbleTime").GetValue<float>();

	private static void Postfix(GeyserScript __instance, float __state)
	{
		var now = Traverse.Create(__instance).Field("rumbleTime").GetValue<float>();
		if (now <= 0f || __state > 0f)
		{
			return; // not the idle → rumbling transition (the cooldown gate denied it)
		}

		PatchBridge.Impl?.OnTrapTriggered(EntityEventKind.GeyserActivated, __instance.transform.position, 0);
	}
}
