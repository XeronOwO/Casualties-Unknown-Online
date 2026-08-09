using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Pickup-feasibility gate for drag-drops (Body.cs:1356). Two parts:
/// 1. FIX: taking an item out of the OPEN container is always feasible — the
///    player explicitly opened its UI (they stand right next to it), and the
///    distance/line-of-sight check against the item's world position (inside
///    the ground container) otherwise refuses the takeout ("cannot take items
///    out of a ground container — picking it up first works").
/// 2. Diagnostics: a failed check elsewhere logs the reason (distance /
///    line-of-sight) so remaining takeout failures are observable.
/// </summary>
[HarmonyPatch(typeof(Body), "DoPickupCheck")]
internal static class DoPickupCheckPatch
{
	private static bool Prefix(Body __instance, Item item, ref bool __result, out (float Distance, bool Blocked) __state)
	{
		var pos = item.transform.position;
		__state = (Vector2.Distance(pos, __instance.transform.position),
			Physics2D.Linecast(__instance.transform.position, pos, LayerMask.GetMask("Ground")));

		var camera = PlayerCamera.main;
		if (camera != null && camera.currentContainer != null // Unity objects — ==; an open container UI
			&& item != null && item.ParentContainer() == camera.currentContainer)
		{
			__result = true; // the open UI already proved proximity — skip the world-space check
			return false;
		}

		return true;
	}

	private static void Postfix(Item item, bool __result, (float Distance, bool Blocked) __state)
	{
		if (!__result && PatchBridge.Impl is { } bridge && bridge.IsSessionActive)
		{
			bridge.OnPickupCheckFailed(item.id, __state.Distance, __state.Blocked);
		}
	}
}
