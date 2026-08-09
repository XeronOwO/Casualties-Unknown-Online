using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Item-use hooks: the two entry points that execute a use action
/// (ItemStats.useAction) — UseItemInHand (the LMB click on the hand item,
/// Body.cs:2449) and UseItem (the radial-menu drag and recipe consumption,
/// Body.cs:2475). Both report the post-use digest so the host can validate and
/// correct the item's state; the report fires only when the use action ACTUALLY
/// ran (the prefix mirrors the caller's guard — UseItemInHand falls through to
/// an attack when the hand is not usable).
/// </summary>
internal static class UseItemPatches
{
	[HarmonyPatch(typeof(Body), "UseItemInHand")]
	internal static class UseItemInHandPatch
	{
		// Mirror the guard clauses of UseItemInHand (Body.cs:2449-2456): the
		// else branch is an attack, not a use. __state carries the item so the
		// postfix never re-reads the hand (a consumable may be gone by then).
		private static void Prefix(Body __instance, out Item? __state)
		{
			var hand = __instance.handSlot;
			var item = __instance.HoldingItem(hand) ? __instance.GetItem(hand) : null;
			__state = item != null && __instance.conscious && item.Stats.usable && item.Stats.usableWithLMB
				? item
				: null; // Unity object — == (a destroyed item is not managed-null)
		}

		private static void Postfix(Item? __state)
		{
			if (__state != null) // Unity object — ==
			{
				PatchBridge.Impl?.OnItemUsed(__state);
			}
		}
	}

	[HarmonyPatch(typeof(Body), "UseItem")]
	internal static class UseItemPatch
	{
		private static void Prefix(Item item, out bool __state) => __state = item.Stats.usable;

		private static void Postfix(Item item, bool __state)
		{
			if (__state && item != null) // Unity object — ==
			{
				PatchBridge.Impl?.OnItemUsed(item);
			}
		}
	}
}
