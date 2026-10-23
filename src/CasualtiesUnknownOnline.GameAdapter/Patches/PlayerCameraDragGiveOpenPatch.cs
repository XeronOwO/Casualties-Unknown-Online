using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The give gesture's entry: while the local player drags an item in the WORLD, the player whose detection range
/// the cursor enters has their backpack opened for the drag.
///
/// <para>
/// The entry is world-only (user ruling 2026-10-23): while the player's own ring or one of their own panels is
/// open, the range is not read at all, so a drag that means "operate my own inventory" can never open somebody
/// else's backpack. Closing the own ring during the drag (the game's own toggle, one press = one meaning) is what
/// enters the world context; the mode is read live every frame, because the native while-dragging body can force
/// the ring open when the cursor crosses a body collider.
/// </para>
///
/// <para>
/// It is the user's 2026-10-22 ruling refined, and it exists because the alternative entry — opening the player
/// panel and pressing "open backpack" mid-drag — works but needs a hotkey no player has a reason to know. The
/// gesture teaches itself: drag the item onto a teammate with the bag closed, the teammate's backpack opens, and a
/// release on a slot gives / on the centre uses.
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
	private static void Prefix(PlayerCamera __instance)
	{
		if (GiveGestureModePolicy.Read(__instance) == GiveGestureMode.OwnUi)
		{
			// The own UI is not a cross-player surface, but the drag's selection must still END here: a drag that
			// stops with the own ring/panel open never reaches the item branch below again, and the next gesture
			// must start from a clean selection (AutoOpenFor(null) is the one place that resets it).
			if (__instance.dragItem == null) // Unity object — ==
			{
				PatchBridge.Impl?.AutoOpenRemoteBackpackForDrag(null);
			}

			return;
		}

		PatchBridge.Impl?.AutoOpenRemoteBackpackForDrag(__instance.dragItem);
	}
}
