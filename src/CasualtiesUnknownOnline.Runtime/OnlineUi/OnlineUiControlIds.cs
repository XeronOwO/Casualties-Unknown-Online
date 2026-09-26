namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// The ids of the controls the Online UI window's own chrome carries (ticket
/// online-ui-art-and-controls-overhaul, S2b). The surface builds the shell's controls — the frame, the
/// title bar, the close button — because they are chrome, and the plugin decides what they mean, because
/// that is where every action lives; an id both halves address is named here instead of being spelled out
/// on each side.
/// </summary>
public static class OnlineUiControlIds
{
	/// <summary>The window's close control: the plugin closes the window when its click arrives.</summary>
	public const string WindowClose = "window.close";
}
