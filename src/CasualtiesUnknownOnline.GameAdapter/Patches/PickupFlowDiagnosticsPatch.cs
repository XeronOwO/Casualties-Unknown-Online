using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// One-shot diagnostics for the container-takeout failure ("cannot take items
/// out of a ground container, and the container ends up empty"): which path a
/// drag release actually took and where the item ended up. Everything logs in
/// ONE pass — no add-a-log-ask-to-retest loop.
/// </summary>
internal static class PickupFlowDiagnosticsPatch
{
	[HarmonyPatch(typeof(PlayerCamera), "TryPerformWorldActions")]
	internal static class TryPerformWorldActionsPatch
	{
		private static void Prefix()
		{
			if (PatchBridge.Impl is { } bridge && bridge.IsSessionActive)
			{
				bridge.OnDragReleasedToWorld();
			}
		}
	}

	[HarmonyPatch(typeof(Body), "PickUpItem")]
	internal static class PickUpItemPatch
	{
		private static void Postfix(Item item, int slot)
		{
			if (PatchBridge.Impl is { } bridge && bridge.IsSessionActive)
			{
				// Where did the item actually end up (in the slot, still in a
				// container, or free in the world)?
				string home;
				if (item.transform.parent != null && item.transform.parent.GetComponent<InventorySlot>() != null)
				{
					home = "slot";
				}
				else if (item.transform.parent != null && item.transform.parent.GetComponent<Container>() != null)
				{
					home = "container";
				}
				else
				{
					home = "world";
				}

				bridge.OnPickUpResult(item.id, slot, home, item.transform.position);
			}
		}
	}
}
