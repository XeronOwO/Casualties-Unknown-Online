namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// Reads the give gesture's context (user ruling 2026-10-23) from the game's own state every frame, never from
/// the last input that changed it: the native while-dragging body forces the ring open when the cursor crosses a
/// body collider, so a mode inferred from "the player pressed Tab" would be wrong the moment the native path
/// changes it. The decision is pure and its own tests pin the matrix; this type only feeds it the live facts.
/// </summary>
internal static class GiveGestureModePolicy
{
	/// <summary>The pure decision: own UI wins over a displayed backpack, and a displayed backpack wins over the world.</summary>
	internal static GiveGestureMode Decide(bool ownUiOpen, bool remoteViewOpen)
	{
		if (ownUiOpen)
		{
			return GiveGestureMode.OwnUi;
		}

		return remoteViewOpen ? GiveGestureMode.DisplayedBackpack : GiveGestureMode.World;
	}

	/// <summary>This client's live context.</summary>
	internal static GiveGestureMode Read(PlayerCamera? camera)
	{
		if (camera == null) // Unity object — ==
		{
			// No camera is no player context: do not cross to another player (both callers pass a live
			// PlayerCamera, so this is the safe default rather than a reachable path).
			return GiveGestureMode.OwnUi;
		}

		var remoteView = RemoteBackpackView.IsOpen;
		// A displayed backpack already implies radialOpen; the ring open WITHOUT that focus is the player's own.
		var ownRing = camera.radialOpen && !remoteView;
		var ownPanel = (camera.woundView != null && camera.woundView.activeSelf) // Unity objects — ==
			|| (camera.containerMenu != null && camera.containerMenu.activeSelf)
			|| (camera.craftingPanel != null && camera.craftingPanel.activeSelf)
			|| (camera.tradeMenu != null && camera.tradeMenu.activeSelf);
		return Decide(ownRing || ownPanel, remoteView);
	}
}
