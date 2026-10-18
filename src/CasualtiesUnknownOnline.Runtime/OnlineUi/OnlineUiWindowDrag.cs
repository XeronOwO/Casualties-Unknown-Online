using System;

namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// Where the window may be dragged on the canvas it hangs on (ticket online-ui-layout-and-input-detail-pass,
/// row 9). The window is the player's own frame, so it may be moved almost anywhere, but it must always be
/// possible to grab it again — and the one grab target is its TITLE BAR, not the frame at large. The rule this
/// replaces kept a strip of the frame on the canvas on every side, which at the top edge was the window's
/// bottom: the title bar ended 411 screen pixels (616 canvas units) above the canvas and the window could not
/// be grabbed again (batch `20261018-t`).
///
/// <para>
/// It is pure and lives in the Runtime for the reason <see cref="OnlineUiRowLayout"/> does: the shape is
/// decided and tested here, without a Unity runtime, and the adapter only reads the answer. The window is
/// anchored at the canvas's centre, so its position is symmetric about it; the canvas rect's own origin is
/// the centre of the rect the window's parent anchors to.
/// </para>
/// </summary>
public static class OnlineUiWindowDrag
{
	/// <summary>How much of the window stays inside the canvas on a side the player can still see through —
	/// the bottom and both sides, where any strip of the frame is a strip one can grab. The title bar is the
	/// one band that is actually draggable, which is why the top edge measures against IT instead.</summary>
	public const float Reach = 48f;

	/// <summary>
	/// The window's anchored position after one drag delta, in the canvas's own units: <paramref name="x"/>
	/// and <paramref name="y"/> are where the drag would put it, and the answer is the nearest position the
	/// rule allows. On the bottom and both sides a strip of the frame stays inside, so a click can land on it
	/// again; on the TOP the strip is the title bar's own, because a window whose title bar is above the canvas
	/// has no surface left that starts a drag.
	/// </summary>
	public static (float X, float Y) Clamp(
		float canvasWidth,
		float canvasHeight,
		float windowWidth,
		float windowHeight,
		float x,
		float y)
	{
		var halfWindowWidth = windowWidth * 0.5f;
		var halfWindowHeight = windowHeight * 0.5f;
		var limitX = Math.Max(0f, (canvasWidth * 0.5f) + halfWindowWidth - Reach);

		// The top edge measures the title bar, not the frame: the window may rise until the last reachable
		// strip of the bar would leave. `Reach` is a frame's strip, so it is capped at the bar's own height —
		// a title bar shorter than `Reach` keeps its whole height, not a strip wider than the band.
		var titleReach = Math.Min(Reach, OnlineUiWindowLayout.TitleHeight);
		var limitTop = Math.Max(
			0f,
			(canvasHeight * 0.5f) - halfWindowHeight + (OnlineUiWindowLayout.TitleHeight - titleReach));
		var limitBottom = Math.Max(0f, (canvasHeight * 0.5f) + halfWindowHeight - Reach);
		return (Clamp(x, -limitX, limitX), Clamp(y, -limitBottom, limitTop));
	}

	private static float Clamp(float value, float min, float max) => value < min ? min : value > max ? max : value;
}
