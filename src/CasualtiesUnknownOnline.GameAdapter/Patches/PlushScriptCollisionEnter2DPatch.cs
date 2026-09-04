using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// <c>PlushScript.OnCollisionEnter2D</c> (PlushScript.cs:17-23) squeaks on any
/// impact above its low velocity threshold. A guest plushie copy is the same
/// non-authoritative presentation case; only the player's explicit use action
/// may squeak locally.
/// </summary>
[HarmonyPatch(typeof(PlushScript), "OnCollisionEnter2D")]
internal static class PlushScriptCollisionEnter2DPatch
{
	private static bool Prefix(PlushScript __instance)
	{
		var item = __instance.GetComponent<Item>();
		if (item != null && NonAuthoritativeItemImpactGuard.Suppress(item, "PlushScript.OnCollisionEnter2D")) // Unity object — ==
		{
			return false;
		}

		return true;
	}
}
