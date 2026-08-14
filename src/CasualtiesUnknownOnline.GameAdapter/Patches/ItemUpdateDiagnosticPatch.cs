using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The Item.Update NRE hunt: a menu-scene burst (1167 lines in 2 s — every
/// frame, ~15 items) whose stack trace is bare Item.Update. The method's
/// nullable dereferences are rb (Awake's GetComponent<Rigidbody2D> — nullable)
/// and WorldGeneration.world (absent in the menu scene). Report the culprit
/// once per object — the domain dedupes, the patch stays a thin adapter.
/// </summary>
[HarmonyPatch(typeof(Item), "Update")]
internal static class ItemUpdateDiagnosticPatch
{
	private static void Prefix(Item __instance)
	{
		// Unity objects — == (a destroyed rb reads as null; a destroyed item
		// never reaches this method).
		if (__instance.rb == null || WorldGeneration.world == null)
		{
			PatchBridge.Impl?.OnBrokenItemUpdate(__instance,
				__instance.rb == null ? "rb-null" : "world-null");
		}
	}
}
