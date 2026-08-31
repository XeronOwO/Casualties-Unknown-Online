namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// Pure layout for above-head nameplates. Kept in the Runtime (no UnityEngine
/// dependency) so the Online UI's nameplate positioning is covered by L0 unit
/// tests instead of requiring a live camera or game window.
/// </summary>
public static class NameplateLayout
{
	/// <summary>The nameplate's horizontal box width in screen pixels.</summary>
	public const float Width = 180f;

	/// <summary>The nameplate's vertical box height in screen pixels.</summary>
	public const float Height = 24f;

	/// <summary>Pixels left free between the projected head point and the bottom edge of the nameplate box.</summary>
	public const float HeadGapPx = 8f;

	/// <summary>
	/// Places a nameplate directly above a projected head point, centered
	/// horizontally on that point.
	/// </summary>
	public static NameplateRect AboveHead(float headX, float headY) =>
		new(headX - (Width * 0.5f), headY - Height - HeadGapPx, Width, Height);
}
