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

	/// <summary>
	/// Decay must not advance while the world is still generating: decay is
	/// Time.deltaTime-driven (Item.cs:205) and each side's generation finishes
	/// at its own pace — a guest's items would start decaying seconds before the
	/// start gate releases both sides together, leaving a permanent condition
	/// offset between the peers ("same items, different rot"). With the gate
	/// freezing timeScale 0 while waiting (StartGateCoordinator), skipping decay
	/// during generation makes every side's items decay from the SAME moment.
	/// </summary>
	[HarmonyPatch(typeof(Item), "HandleDecay")]
	internal static class ItemHandleDecayPatch
	{
		private static bool Prefix() => !HarmonyTraverse.IsGenerating();
	}
}
