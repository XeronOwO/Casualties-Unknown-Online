using CasualtiesUnknownOnline.Runtime.Protocol;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Battery charger → BatteryInserted: the first successful insert consumed the
/// one-shot mp3 gift (firstTime flipped, BatteryRecharger.cs:68-74). The
/// insert itself rides the item domain (the battery IS a world item — its
/// position and condition sync there); this event only consumes the gift on
/// the peers, so the mp3 is given exactly once per charger, not once per side.
/// </summary>
[HarmonyPatch(typeof(BatteryRecharger), "OnUse")]
internal static class TrapBatteryRechargerPatch
{
	private static void Prefix(BatteryRecharger __instance, out bool __state) =>
		__state = Traverse.Create(__instance).Field("firstTime").GetValue<bool>();

	private static void Postfix(BatteryRecharger __instance, bool __state)
	{
		if (!__state || Traverse.Create(__instance).Field("firstTime").GetValue<bool>())
		{
			return; // firstTime did not just flip false (no insert, or already consumed)
		}

		PatchBridge.Impl?.OnTrapTriggered(EntityEventKind.BatteryInserted, __instance.transform.position, 0);
	}
}
