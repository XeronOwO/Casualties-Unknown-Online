using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.OnlineUi;

/// <summary>
/// Marks the hierarchy CUO's own live Online UI surface owns (ticket online-ui-art-and-controls-overhaul:
/// S2a built the surface, S4 makes its ownership answerable). The migrated controls are uGUI and block
/// their own pixels, so CUO's own input guard must never lay a raycast blocker over them: that guard
/// exists for surfaces the game's <c>EventSystem</c> cannot see, and one blocker over CUO's own canvas
/// covers the launcher and the whole window, leaving every click the player makes on them unanswered.
/// </summary>
internal sealed class OnlineUiSurfaceMarker : MonoBehaviour
{
	/// <summary>
	/// True when the component belongs to CUO's own surface — its canvas, or a control under it. The
	/// question is asked by the input guard's sweeps, so it is one rule for every sweep rather than a
	/// name check per call site.
	/// </summary>
	internal static bool IsInside(Component component) =>
		component != null // Unity object — ==
		&& component.GetComponentInParent<OnlineUiSurfaceMarker>() != null;
}
