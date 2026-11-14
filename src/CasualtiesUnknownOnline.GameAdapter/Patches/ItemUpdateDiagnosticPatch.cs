using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The Item.Update NRE hunt: a menu-scene burst (1167 lines in 2 s — every
/// frame, ~15 items) whose stack trace is bare Item.Update. The method's
/// nullable dereferences are rb (Awake's GetComponent<Rigidbody2D> — nullable)
/// and WorldGeneration.world (absent in the menu scene). Report the culprit
/// once per object, then skip the native body: it dereferences both on its
/// first line, so running it again only repeats the exception every frame.
/// </summary>
[HarmonyPatch(typeof(Item), "Update")]
internal static class ItemUpdateDiagnosticPatch
{
	private static bool Prefix(Item __instance)
	{
		// Unity objects — == (a destroyed rb reads as null; a destroyed item
		// never reaches this method).
		if (__instance.rb == null || WorldGeneration.world == null)
		{
			PatchBridge.Impl?.OnBrokenItemUpdate(__instance,
				__instance.rb == null ? "rb-null" : "world-null");
			return false;
		}

		return true;
	}
}
