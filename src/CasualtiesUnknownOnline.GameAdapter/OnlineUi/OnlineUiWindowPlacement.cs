using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.OnlineUi;

/// <summary>
/// Keeps the Online UI window on the canvas it hangs on (ticket online-ui-layout-and-input-detail-pass, S1 and
/// the review that followed it): the frame is larger than the game's own panels, so a canvas smaller than it —
/// a low resolution, a large game UI scale — must still hold the whole window, and a canvas that changes size
/// after the window was built must be followed.
///
/// <para>
/// It is a type of its own because it is the one piece of the window's geometry that is not a band's declared
/// size: a fit measured against the canvas's own rect, which is also why it is the piece a later cycle can test
/// without a Unity runtime.
/// </para>
/// </summary>
internal static class OnlineUiWindowPlacement
{
	/// <summary>The smallest the window may become when the canvas is smaller than the frame: below this the
	/// page stops being usable, so the frame keeps this size and overflows instead.</summary>
	internal const float MinWidth = 620f;

	internal const float MinHeight = 420f;

	/// <summary>The room the frame keeps from the canvas edge when it is clamped to it.</summary>
	internal const float CanvasMargin = 16f;

	/// <summary>The window's own rect for the canvas it is about to hang on: the size it asks for, clamped into
	/// that canvas and never below the floor. A host that is not a canvas (the game has none yet, or it is still
	/// being built) leaves the size alone.</summary>
	internal static Vector2 Fit(Transform? canvasHost, Vector2 desired)
	{
		if (canvasHost is not RectTransform canvas || canvas.rect.width <= 0f || canvas.rect.height <= 0f)
		{
			return desired;
		}

		return new Vector2(
			Mathf.Max(MinWidth, Mathf.Min(desired.x, canvas.rect.width - (2f * CanvasMargin))),
			Mathf.Max(MinHeight, Mathf.Min(desired.y, canvas.rect.height - (2f * CanvasMargin))));
	}
}
