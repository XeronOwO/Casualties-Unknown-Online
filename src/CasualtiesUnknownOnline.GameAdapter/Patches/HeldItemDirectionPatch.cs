using CasualtiesUnknownOnline.GameAdapter.Character;
using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Remote render clones: point hand-held directional items at the peer's
/// reported aim instead of the local mouse (#119). The original
/// CustomItemBehaviour.Update orients flashlight (CustomItemBehaviour.cs:526),
/// emergencylight (:439) and rangefinder (:512) with
/// Camera.main.ScreenToWorldPoint(Input.mousePosition) — on a clone that is
/// whoever is playing on THIS machine, not the owner. The fix is a Postfix:
/// the game's other per-item Update work stays untouched, and only the
/// orientation write is corrected for clone bodies (the RemoteBodyDriver
/// marker) after the first 20 Hz entity snapshot arrived (LastStateMs — before
/// that SessionStatePump has not written the peer's aim yet).
/// </summary>
[HarmonyPatch(typeof(CustomItemBehaviour), "Update")]
internal static class HeldItemDirectionPatch
{
	private static void Postfix(CustomItemBehaviour __instance)
	{
		var item = __instance.GetComponent<Item>();
		if (item == null) // Unity object — == (is null misses destroyed)
		{
			return;
		}

		float offset;
		switch (item.id)
		{
			case "flashlight":
			case "emergencylight":
				offset = HeldItemDirection.LightAngleOffsetDegrees;
				break;
			case "rangefinder":
				offset = HeldItemDirection.SightAngleOffsetDegrees;
				break;
			default:
				return;
		}

		var parent = __instance.transform.parent;
		if (parent == null) // Unity object — ==
		{
			return;
		}

		var slot = parent.GetComponent<InventorySlot>();
		if (slot == null || slot.body == null) // Unity object — ==
		{
			return;
		}

		var body = slot.body;
		if (slot != body.slots[0] && slot != body.slots[1])
		{
			return; // the original only aims items held in the two hand slots
		}

		if (!body.TryGetComponent<RemoteBodyDriver>(out var driver) || driver.LastStateMs == 0)
		{
			return; // local body, or the clone has not received the owner's aim yet
		}

		var pos = __instance.transform.position;
		var look = body.targetLookPos;
		__instance.transform.eulerAngles = new Vector3(
			0f,
			0f,
			HeldItemDirection.AngleFor(pos.x, pos.y, look.x, look.y, offset));
	}
}
