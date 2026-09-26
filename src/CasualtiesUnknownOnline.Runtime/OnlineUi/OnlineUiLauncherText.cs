namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// The Online UI launcher's caption: the translated label plus the open/closed marker the launcher has
/// always carried. Pure, so the marker survives the move off IMGUI — the click now happens on the game's
/// own button, and the only thing that still travels from the Runtime to that button is this string
/// (ticket online-ui-art-and-controls-overhaul, S2a).
/// </summary>
public static class OnlineUiLauncherText
{
	/// <summary>The marker shown while the Online UI window is open.</summary>
	public const string OpenMarker = " ▲";

	/// <summary>The marker shown while the Online UI window is closed.</summary>
	public const string ClosedMarker = " ▼";

	/// <summary>The launcher's caption for the current window state.</summary>
	public static string Label(string caption, bool open) =>
		caption + (open ? OpenMarker : ClosedMarker);
}
