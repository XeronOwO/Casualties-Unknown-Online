using CasualtiesUnknownOnline.GameAdapter.Items;
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

	[HarmonyPatch(typeof(Item), "Awake")]
	internal static class ItemAwakePatch
	{
		private static void Postfix(Item __instance)
		{
			// Awake runs synchronously inside Object.Instantiate, so the
			// BuildingEntity.Update death-branch scope is still active here.
			// The marker is consumed at Item.Start (next frame) by
			// ItemWorldSync.OnItemInstantiated.
			if (CallContext.Current != CallContext.Origin.BuildingDeathDrop)
			{
				return;
			}

			__instance.gameObject.AddComponent<BuildingDeathDropOrigin>();
		}
	}

	[HarmonyPatch(typeof(Item), "OnDestroy")]
	internal static class ItemOnDestroyPatch
	{
		private static void Postfix(Item __instance)
		{
			// A crafting material's destroy is part of the craft: the in-scope
			// destroy is silenced by the scope, and the END-OF-FRAME destroy
			// (Unity defers Object.Destroy past the scope) is silenced by the
			// coordinator's destroy-claim set — the fact rode the craft report.
			if (CallContext.Current == CallContext.Origin.Craft || PatchBridge.Impl?.ShouldSuppressDestroy(__instance) == true)
			{
				return;
			}

			PatchBridge.Impl?.OnItemDestroyed(__instance);
		}
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
