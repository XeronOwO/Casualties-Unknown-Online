using System;

namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// Where a marker's LABEL sits, once the marker itself has been clamped to the screen's edge
/// (<see cref="OffScreenArrowGeometry"/>). The arrow is a fixed-width glyph, so the arrow's own placement is
/// where its box goes; the label beside it is a name and a distance, whose width nothing knows in advance,
/// so it needs a box of its own rather than the arrow's position.
///
/// <para>
/// The rule is one clamp per axis against the screen's guarded rectangle, in the caller's own units — the
/// same convention <see cref="OffScreenArrowGeometry"/> uses, and for the same reason: a hard-coded pixel
/// margin would only move the overflow on a resized window instead of removing it.
/// </para>
///
/// <para>
/// When the label is wider than the room the screen has, clamping to one border would push the whole
/// overflow onto that border. This rule pins it to the screen's centre on that axis instead, so the text
/// runs under both edges evenly and reports <see cref="OffScreenLabelPlacement.Pinned"/> — the caller decides
/// whether to say so; this rule never truncates text, which is a naming policy rather than a placement one.
/// </para>
/// </summary>
public static class OffScreenLabelGeometry
{
	/// <summary>
	/// Map a label's desired centre (the arrow's own clamped position plus the gap the caller wants) to a
	/// centre whose box stays inside the screen's guarded rectangle. The answer is per-edge without knowing
	/// the edge: the arrow's position already carries which border it was pinned to, so clamping the label's
	/// own box against the same rectangle moves a right-edge label inboard and leaves an up/down edge
	/// label in the arrow's own column, because that column is already inside the rectangle.
	/// </summary>
	public static OffScreenLabelPlacement Place(
		float centerX,
		float centerY,
		float labelWidth,
		float labelHeight,
		float screenWidth,
		float screenHeight,
		float margin)
	{
		if (screenWidth <= 0f || screenHeight <= 0f || margin < 0f)
		{
			return new OffScreenLabelPlacement(centerX, centerY, false);
		}

		var halfWidth = Math.Max(0f, labelWidth * 0.5f);
		var halfHeight = Math.Max(0f, labelHeight * 0.5f);
		return new OffScreenLabelPlacement(
			PlaceAxis(centerX, screenWidth, margin, halfWidth),
			PlaceAxis(centerY, screenHeight, margin, halfHeight),
			halfWidth * 2f >= screenWidth - (margin * 2f));
	}

	private static float PlaceAxis(float center, float screen, float margin, float half)
	{
		var usable = screen - (margin * 2f);
		if (half * 2f >= usable)
		{
			// No room on either side: the centre of the screen, so the overflow is shared by both edges
			// rather than landing entirely on one.
			return screen * 0.5f;
		}

		var min = margin + half;
		var max = screen - margin - half;
		return center < min ? min : center > max ? max : center;
	}
}
