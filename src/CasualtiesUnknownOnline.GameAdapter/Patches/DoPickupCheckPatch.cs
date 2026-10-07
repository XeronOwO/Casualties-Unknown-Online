using CasualtiesUnknownOnline.GameAdapter.Items;
using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Pickup-feasibility gate for drag-drops (Body.cs:1356). Three parts:
/// 1. REFUSAL: a standing item object is never reachable by a local gesture. This
///    is the one gate the pickup, the ground wear and the recipe probe all pass
///    through, so the rule lives here — the disabled collider keeps such an
///    object out of the queries anyway, and this makes the refusal the rule
///    rather than an accident of the prefab.
/// 2. FIX: taking an item out of the OPEN container is always feasible — the
///    player explicitly opened its UI (they stand right next to it), and the
///    distance/line-of-sight check against the item's world position (inside
///    the ground container) otherwise refuses the takeout ("cannot take items
///    out of a ground container — picking it up first works").
/// 3. Diagnostics: a failed check elsewhere logs the reason (distance /
///    line-of-sight) so remaining takeout failures are observable — and a
///    refusal by part 1 is reported as itself, never as a distance failure.
/// </summary>
[HarmonyPatch(typeof(Body), "DoPickupCheck")]
internal static class DoPickupCheckPatch
{
	private static bool Prefix(Body __instance, Item item, ref bool __result, out CheckState __state)
	{
		var pos = item.transform.position;
		__state = new CheckState(
			Vector2.Distance(pos, __instance.transform.position),
			Physics2D.Linecast(__instance.transform.position, pos, LayerMask.GetMask("Ground")),
			Standing: StandingItems.Is(item));

		if (__state.Standing)
		{
			__result = false;
			return false;
		}

		var camera = PlayerCamera.main;
		if (camera != null && camera.currentContainer != null // Unity objects — ==; an open container UI
			&& item != null && item.ParentContainer() == camera.currentContainer)
		{
			__result = true; // the open UI already proved proximity — skip the world-space check
			return false;
		}

		return true;
	}

	private static void Postfix(Item item, bool __result, CheckState __state)
	{
		if (__result || PatchBridge.Impl is not { IsSessionActive: true } bridge)
		{
			return;
		}

		// The two refusals are told apart by the prefix's capture, so neither can
		// report the other's reason.
		if (__state.Standing)
		{
			PatchBridge.ItemCategory?.ReportStandingItemGestureRefused(item, "DoPickupCheck");
			return;
		}

		bridge.OnPickupCheckFailed(item.id, __state.Distance, __state.Blocked);
	}

	/// <summary>Per-call state across the patch pair (Harmony __state — never a static field): the diagnostic's two numbers, plus which refusal this was.</summary>
	private readonly record struct CheckState(float Distance, bool Blocked, bool Standing);
}
