namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>The screen-edge placement for one off-screen marker.</summary>
public enum OffScreenArrowDirection
{
	/// <summary>The point is inside the guarded screen bounds — no arrow is needed.</summary>
	None,

	Up,
	Down,
	Left,
	Right,
}
