using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The give gesture's entry: while the local player drags an item from its own backpack, the player whose
/// detection range the cursor enters has their backpack opened for the drag.
///
/// <para>
/// It is the user's 2026-10-22 ruling, and it exists because the alternative entry — opening the player panel
/// and pressing "open backpack" mid-drag — works but needs a hotkey no player has a reason to know. The
/// gesture teaches itself: dragging the item onto a teammate shows that teammate's backpack, and the release
/// either uses the item on them (inside the range) or gives it (on the displayed backpack).
/// </para>
///
/// <para>
/// Its own patch class rather than an arm of <see cref="PlayerCameraDragUseCuePatch"/>
/// (<c>PlayerCameraHandleWhileDraggingPatch</c> is already patched on this method too): the cue reads the
/// gesture, this one acts on it, and each stays one concern.
/// </para>
/// </summary>
[HarmonyPatch(typeof(PlayerCamera), "HandleWhileDragging")]
internal static class PlayerCameraDragGiveOpenPatch
{
	private static void Prefix(PlayerCamera __instance) =>
		PatchBridge.Impl?.AutoOpenRemoteBackpackForDrag(__instance.dragItem);
}
