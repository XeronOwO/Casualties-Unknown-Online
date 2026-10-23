using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Keeps the drag gesture's cue current, once per while-dragging frame. The gesture is the
/// only entry point for using an item on a teammate and it announced nothing — the user
/// read that as a feature that does not exist and a misfire as a bug (finding 2026-10-06),
/// and the ruling that followed is that the cue must NAME the player.
/// <para>
/// It reports the game's own dragged item and decides nothing: a null item is a drag that has
/// ended, which is what clears the cue. The tracker names the displayed player only while the
/// pointer is on the displayed backpack's centre (user ruling 2026-10-23), so a release on a
/// slot — a give — shows no USE label. The decision itself is the tracker's, and the drawing
/// is the plugin's.
/// </para>
/// <para>
/// It is its OWN patch class rather than another arm of
/// <see cref="PlayerCameraHandleWhileDraggingPatch"/>, which is already patched on this
/// method and carries the favourite-mark logic: two unrelated concerns on one method stay
/// two classes.
/// </para>
/// </summary>
[HarmonyPatch(typeof(PlayerCamera), "HandleWhileDragging")]
internal static class PlayerCameraDragUseCuePatch
{
	private static void Prefix(PlayerCamera __instance) =>
		PatchBridge.DragUseCue?.UpdateDragUseCue(__instance.dragItem);
}
