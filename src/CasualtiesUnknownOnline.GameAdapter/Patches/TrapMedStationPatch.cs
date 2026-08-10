using CasualtiesUnknownOnline.Runtime.Protocol;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Med station → MedStationHealed: a body entered the trigger and the one-shot
/// didHeal flipped (MedStationScript.cs:19-29 — the station heals, Backgroundify()s
/// and is spent). Pure observation; the event replays the consume on the peers.
/// </summary>
[HarmonyPatch(typeof(MedStationScript), "OnTriggerEnter2D")]
internal static class TrapMedStationPatch
{
	private static void Prefix(MedStationScript __instance, out bool __state) =>
		__state = Traverse.Create(__instance).Field("didHeal").GetValue<bool>();

	private static void Postfix(MedStationScript __instance, bool __state)
	{
		if (__state || !Traverse.Create(__instance).Field("didHeal").GetValue<bool>())
		{
			return; // not the false → true transition
		}

		PatchBridge.Impl?.OnTrapTriggered(EntityEventKind.MedStationHealed, __instance.transform.position, 0);
	}
}
