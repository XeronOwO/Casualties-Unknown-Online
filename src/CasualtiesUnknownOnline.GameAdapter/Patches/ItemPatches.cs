using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Item lifecycle hooks — the unified entry for every item that exists
/// (drops, creature loot, use-spawned items all go through Item.Start). The
/// adapter decides generation-time (skipped — world-gen determinism) vs
/// runtime (allocates an instance id and reports). OnDestroy reports the
/// runtime destroy (decay to zero, consumed).
/// </summary>
internal static class ItemPatches
{
	[HarmonyPatch(typeof(Item), "Start")]
	internal static class ItemStartPatch
	{
		private static void Postfix(Item __instance) => PatchBridge.Impl?.OnItemInstantiated(__instance);
	}

	[HarmonyPatch(typeof(Item), "OnDestroy")]
	internal static class ItemOnDestroyPatch
	{
		private static void Postfix(Item __instance) => PatchBridge.Impl?.OnItemDestroyed(__instance);
	}
}
