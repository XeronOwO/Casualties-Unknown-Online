using CasualtiesUnknownOnline.Runtime.Protocol;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Trade hooks — thin adapters, zero cross-call state (user rule). The
/// interaction Postfixes report a locally-executed action (the game method ran
/// in full); the coordinator (TradeStateSync) splits host-broadcast vs
/// guest-report. The guest-side no-ops keep the trader's stock and death drops
/// host-authoritative: the acting side's stock generation would roll its own
/// (Start runs in the frame gap, outside the isolated generation stream) and a
/// duplicate death drop would double the loot (the host's drop is synced by
/// the item domain).
/// </summary>
internal static class TraderPatches
{
	/// <summary>Guest in a live session: the trader's stock generation is skipped — the host's snapshot supplies the stock.</summary>
	[HarmonyPatch(typeof(TraderScript), "GenerateInventory")]
	internal static class TraderStartPatch
	{
		private static bool Prefix(TraderScript __instance)
		{
			if (PatchBridge.Impl is not { } bridge || !bridge.IsSessionActive || bridge.IsHostMode)
			{
				return true;
			}

			__instance.items = []; // empty until the host's authoritative snapshot arrives (world entry / 5 s fallback)
			return false;
		}
	}

	/// <summary>Guest in a live session: the death drop is skipped — the host drops alone and the item domain syncs the loot (a second local roll would double it).</summary>
	[HarmonyPatch(typeof(TraderScript), "DropInventory")]
	internal static class TraderDropInventoryPatch
	{
		private static bool Prefix()
		{
			if (PatchBridge.Impl is not { } bridge)
			{
				return true;
			}

			return !bridge.IsSessionActive || bridge.IsHostMode;
		}
	}

	/// <summary>TryPurchase: report the locally-executed purchase with the newly created
	/// item (the backpack hold for the rejected-purchase rollback — found by the
	/// slot-count baseline: the item's Start runs a frame later, so Item.allItems
	/// is not yet registered when the Postfix runs).</summary>
	[HarmonyPatch(typeof(TraderScript), "TryPurchase")]
	internal static class TryPurchasePatch
	{
		private static void Prefix(TraderScript __instance, TraderItem item, out int __state) =>
			__state = __instance != null ? CountInBackpack(item.id) : 0; // Unity object — ==

		private static void Postfix(TraderScript __instance, TraderItem item, int __state) =>
			PatchBridge.Impl?.OnTraderActionReported(__instance, TraderActionKind.Purchase, item.id, 0, FindNewItem(item.id, __state));

		private static int CountInBackpack(string id)
		{
			var body = PlayerCamera.main.body;
			if (body == null) // Unity object — ==
			{
				return 0;
			}

			var count = 0;
			for (var i = 0; i < body.slots.Length; i++)
			{
				var item = body.GetItem(i);
				if (item != null && item.id == id) // Unity object — ==
				{
					count++;
				}
			}

			return count;
		}

		private static Item? FindNewItem(string id, int before)
		{
			var body = PlayerCamera.main.body;
			if (body == null) // Unity object — ==
			{
				return null;
			}

			var seen = 0;
			for (var i = 0; i < body.slots.Length; i++)
			{
				var item = body.GetItem(i);
				if (item != null && item.id == id) // Unity object — ==
				{
					seen++;
					if (seen == before + 1)
					{
						return item;
					}
				}
			}

			return null;
		}
	}

	[HarmonyPatch(typeof(TraderScript), "GiveItem")]
	internal static class GiveItemPatch
	{
		private static void Postfix(TraderScript __instance, Item item) =>
			PatchBridge.Impl?.OnTraderActionReported(__instance, TraderActionKind.GiveItem, item.id, item.Stats.GetValue(item), null);
	}

	[HarmonyPatch(typeof(TraderScript), "TryHaggle")]
	internal static class TryHagglePatch
	{
		private static void Postfix(TraderScript __instance) =>
			PatchBridge.Impl?.OnTraderActionReported(__instance, TraderActionKind.Haggle, "", 0, null);
	}

	[HarmonyPatch(typeof(TraderScript), "Threaten")]
	internal static class ThreatenPatch
	{
		private static void Postfix(TraderScript __instance) =>
			PatchBridge.Impl?.OnTraderActionReported(__instance, TraderActionKind.Threaten, "", 0, null);
	}

	[HarmonyPatch(typeof(TraderScript), "TryHug")]
	internal static class TryHugPatch
	{
		private static void Postfix(TraderScript __instance) =>
			PatchBridge.Impl?.OnTraderActionReported(__instance, TraderActionKind.Hug, "", 0, null);
	}

	[HarmonyPatch(typeof(TraderScript), "AskToMove")]
	internal static class AskToMovePatch
	{
		private static void Postfix(TraderScript __instance) =>
			PatchBridge.Impl?.OnTraderActionReported(__instance, TraderActionKind.MoveTo, "", 0, null);
	}

	[HarmonyPatch(typeof(TraderScript), "MeetPlayer")]
	internal static class MeetPlayerPatch
	{
		private static void Postfix(TraderScript __instance) =>
			PatchBridge.Impl?.OnTraderActionReported(__instance, TraderActionKind.MeetPlayer, "", 0, null);
	}
}
