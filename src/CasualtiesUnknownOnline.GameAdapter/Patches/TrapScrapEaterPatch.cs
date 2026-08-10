using CasualtiesUnknownOnline.Runtime.Protocol;
using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Scrap eater → ScrapEaterProgress: every successful feed (scrapAmount rose —
/// the metal item was consumed) reports the progress % (0-100; 100 = the
/// unlock ran). The feed itself is item-domain (the scrap metal is a world
/// item); the event carries the progress so the peers' copies show the same
/// gauge and unlock the same doors.
/// </summary>
[HarmonyPatch(typeof(ScrapEaterScript), "OnUse")]
internal static class TrapScrapEaterPatch
{
	private static void Prefix(ScrapEaterScript __instance, out float __state) => __state = __instance.scrapAmount;

	private static void Postfix(ScrapEaterScript __instance, float __state)
	{
		if (__instance.scrapAmount <= __state)
		{
			return; // nothing was fed (deny path)
		}

		var progress = (byte)Mathf.Clamp(__instance.scrapAmount / ScrapEaterScript.target * 100f, 0f, 100f);
		PatchBridge.Impl?.OnTrapTriggered(EntityEventKind.ScrapEaterProgress, __instance.transform.position, progress);
	}
}
