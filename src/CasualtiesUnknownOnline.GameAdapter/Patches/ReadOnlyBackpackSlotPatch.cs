using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The view-only remote backpack's own UI language: the game's red slot state.
/// <c>InvButton.UpdateGraphic</c> already paints a body slot red when that slot's
/// <c>canPickUp</c> answers false (<c>InvButton.cs:137</c>), which is what an
/// unconscious player sees looking at their own pack. The middle access value
/// reproduces that LOOK for a conscious owner by recolouring the button only — it
/// deliberately never changes <c>InventorySlot.canPickUp</c>, because
/// <c>InventorySlot.Update</c> drops an item from a slot whose
/// <c>dropWhenUnconscious</c> flag is set and whose <c>canPickUp</c> turned false, and a
/// view-only DISPLAY must not newly fire a native drop on a conscious clone.
///
/// <para>
/// The recolour is scoped to an OPEN view-only focus and to body slots, so it cannot
/// touch the local ring or an operable remote view. It does not block the drag: the
/// release is refused with the red line by the dispatcher, and the host would refuse
/// it too.
/// </para>
/// </summary>
[HarmonyPatch(typeof(InvButton), nameof(InvButton.UpdateGraphic))]
internal static class ReadOnlyBackpackSlotPatch
{
	private static readonly AccessTools.FieldRef<InvButton, Image> ButtonImage = AccessTools.FieldRefAccess<InvButton, Image>("image");

	private static void Postfix(InvButton __instance)
	{
		if (!RemoteBackpackView.IsOpen
			|| !RemoteBackpackView.IsReadOnly
			|| !__instance.isBody
			|| __instance.woundView)
		{
			return;
		}

		var image = ButtonImage(__instance);
		if (image == null) // Unity object — ==
		{
			return;
		}

		var color = image.color;
		image.color = new Color(1f, 0f, 0f, color.a);
	}
}
