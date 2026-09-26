namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// How much emphasis a label carries, in the Online UI window's own vocabulary (ticket
/// online-ui-art-and-controls-overhaul, S2b). The four levels are the ones the IMGUI drawers used
/// through <c>OnlineUiTheme</c>; the adapter derives size and weight from the game's own row label, so
/// the levels travel as meaning rather than as fonts.
/// </summary>
public enum OnlineUiTextStyle
{
	/// <summary>Body text: the page's ordinary lines.</summary>
	Default,

	/// <summary>Secondary text: hints, counts and the small print under a section.</summary>
	Muted,

	/// <summary>A section heading: what a group of rows below belongs to.</summary>
	Section,

	/// <summary>The window's own title.</summary>
	Title,
}
