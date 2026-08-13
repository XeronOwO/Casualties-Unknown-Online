using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Crafting operation hooks — the ONE-operation-ONE-report funnels. Each
/// patch's prefix opens the operation (a Craft scope + the pre-state snapshot
/// via PatchBridge), the postfix commits the terminal state as one
/// CraftReportMsg. The scope silences the sub-hooks (material DropItem /
/// product PickUpItem / container loads); the end-of-frame destroys ride the
/// destroy-claim set (Unity's Object.Destroy is deferred, so OnDestroy fires
/// after the scope closed — ShouldSuppressDestroy consumes the claim). The
/// state crosses prefix → postfix via Harmony __state (per-call state, never
/// a static field).
/// </summary>
internal static class CraftingPatches
{
	[HarmonyPatch(typeof(Recipe), "TryMake")]
	internal static class RecipeTryMakePatch
	{
		private static void Prefix(Recipe __instance, out object? __state) => __state = PatchBridge.Impl?.OnCraftBegin(__instance);

		private static void Postfix(object? __state)
		{
			PatchBridge.Impl?.OnCraftEnd(__state);
			// The craft changed the inventory (products consumed/created) — the
			// character snapshot baseline must re-report. PickUpItem does NOT
			// fire OnInventoryChanged (only SwapSlots/SwitchHands/WearWearable
			// do), so the coordinator's suppression would silently stale the
			// baseline without this.
			if (__state != null)
			{
				PatchBridge.Impl?.OnInventoryChanged();
			}
		}
	}

	[HarmonyPatch(typeof(Body), "CombineItems")]
	internal static class BodyCombinePatch
	{
		private static void Prefix(Body __instance, Item it1, Item it2, out object? __state) =>
			__state = PatchBridge.Impl?.OnCombineBegin(__instance, it1, it2);

		private static void Postfix(object? __state)
		{
			PatchBridge.Impl?.OnCombineEnd(__state);
			if (__state != null)
			{
				PatchBridge.Impl?.OnInventoryChanged(); // the ammo/mag left the inventory
			}
		}
	}

	/// <summary>
	/// The transfer UI confirmed — the terminal event of the interactive
	/// liquid-transfer (no scope: an overweight target's UnloadItem is a
	/// genuine world fact, its drop report fires naturally).
	/// </summary>
	[HarmonyPatch(typeof(LiquidTransfer), "Finish")]
	internal static class LiquidTransferFinishPatch
	{
		private static void Postfix(LiquidTransfer __instance)
		{
			if (__instance.transferTo != null && __instance.transferFrom != null) // Unity objects — ==
			{
				PatchBridge.Impl?.OnLiquidTransferFinished(__instance.transferTo, __instance.transferFrom);
			}
		}
	}
}
