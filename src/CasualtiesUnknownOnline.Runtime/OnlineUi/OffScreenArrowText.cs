namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// The mark an off-screen marker draws: the arrow that points at a nameplate or a ping that left the
/// screen (ticket online-ui-art-and-controls-overhaul, S6).
///
/// <para>
/// It is a Runtime rule for the reason <see cref="OnlineUiLauncherText"/> is one: the glyph is the answer
/// to a value the Runtime already computes (<see cref="OffScreenArrowGeometry"/>'s direction), and the two
/// IMGUI surfaces that used to draw these markers each carried their own copy of this switch — one place
/// and one test is what keeps the arrow a single fact now that both draw through the same surface.
/// </para>
/// </summary>
public static class OffScreenArrowText
{
	/// <summary>The mark for a marker above the screen.</summary>
	public const string Up = "\u25B2";

	/// <summary>The mark for a marker below the screen.</summary>
	public const string Down = "\u25BC";

	/// <summary>The mark for a marker left of the screen.</summary>
	public const string Left = "\u25C0";

	/// <summary>The mark for a marker right of the screen.</summary>
	public const string Right = "\u25B6";

	/// <summary>The mark for a marker that is on screen after all: the geometry says "no direction", and a
	/// dot is what an arrow with nowhere to point draws as.</summary>
	public const string OnScreen = "\u2022";

	/// <summary>The arrow for one placement direction.</summary>
	public static string Glyph(OffScreenArrowDirection direction) => direction switch
	{
		OffScreenArrowDirection.Up => Up,
		OffScreenArrowDirection.Down => Down,
		OffScreenArrowDirection.Left => Left,
		OffScreenArrowDirection.Right => Right,
		_ => OnScreen,
	};
}
