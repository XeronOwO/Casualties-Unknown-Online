using System;

namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// Where a point-anchored panel's top-left corner goes (ticket online-ui-art-and-controls-overhaul, S5).
///
/// <para>
/// The IMGUI context menu clamped its own rect inside the screen when it drew; the rule is the same one,
/// moved to where it can be tested. Two facts make it a rule rather than arithmetic: the pointer offset is
/// part of the shape (the menu opens down-right of the click), and a panel too large for the screen hangs
/// from the TOP with its first rows visible instead of being pushed off the top edge.
/// </para>
///
/// <para>
/// Every argument is in ONE space, and the caller owns which: the surface passes its own canvas units —
/// the pointer converted into the parent rect once, the panel's own size as the layout produced it, and the
/// screen, which for that surface IS the parent rect it covers. Mixing pixels with canvas units would make
/// the clamp hold only at a canvas scale of 1, which is exactly the defect the independent review found in
/// the first cut of this stage; the surface's scale is the game's UI scale, and nothing here may assume it.
/// The two constants below are therefore in the caller's unit too, which keeps the margin and the offset
/// the same apparent size as the panel they belong to.
/// </para>
/// </summary>
public static class OnlineUiPanelPlacement
{
	/// <summary>The gap a panel keeps from every screen edge (in the caller's unit).</summary>
	public const float ScreenMargin = 4f;

	/// <summary>How far right of the pointer the panel's own corner sits (in the caller's unit).</summary>
	public const float PointerOffsetX = 8f;

	/// <summary>How far above the pointer the panel's own corner sits (the caller's Y grows up).</summary>
	public const float PointerOffsetY = 8f;

	/// <summary>
	/// The panel's top-left corner for a pointer at (<paramref name="pointerX"/>, <paramref name="pointerY"/>),
	/// kept inside a <paramref name="screenWidth"/> by <paramref name="screenHeight"/> area. All six values
	/// are in one space; the corner comes back in it (origin bottom-left, Y up).
	/// </summary>
	public static OnlineUiPanelCorner ForPointer(
		float pointerX,
		float pointerY,
		float width,
		float height,
		float screenWidth,
		float screenHeight)
	{
		var left = Clamp(pointerX + PointerOffsetX, ScreenMargin, screenWidth - width - ScreenMargin);
		var maxTop = screenHeight - ScreenMargin;
		var minTop = Math.Min(height + ScreenMargin, maxTop);
		var top = Clamp(pointerY + PointerOffsetY, minTop, maxTop);
		return new OnlineUiPanelCorner(left, top);
	}

	/// <summary><paramref name="minimum"/> wins when the range is empty — a panel wider or taller than the
	/// screen keeps its own edge on the screen instead of flipping to the far side.</summary>
	private static float Clamp(float value, float minimum, float maximum) =>
		Math.Max(minimum, Math.Min(value, maximum));
}
