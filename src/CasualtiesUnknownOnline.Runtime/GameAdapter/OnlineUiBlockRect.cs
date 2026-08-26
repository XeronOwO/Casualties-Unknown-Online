namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// One screen-space rectangle occupied by a CUO Online UI surface (quick
/// panel, context menu, etc.). Coordinates are IMGUI GUI space: the origin is
/// the top-left of the screen and Y grows downward, matching <c>UnityEngine.Rect</c>
/// values used by the Online UI. The Runtime cannot reference UnityEngine, so
/// this plain value type crosses the adapter boundary instead.
/// </summary>
public readonly record struct OnlineUiBlockRect(
	float X,
	float Y,
	float Width,
	float Height)
{
	/// <summary>True when a GUI-space point (Y down) lies inside this rectangle.</summary>
	public bool Contains(float x, float y) =>
		x >= X
		&& x <= X + Width
		&& y >= Y
		&& y <= Y + Height;
}
