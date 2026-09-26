namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// One fact the Online UI's native surface reports back, in the order it happened. The surface owns no
/// semantics: a click is "the launcher was clicked", not "the window is now open" — the Runtime decides
/// what that means, exactly as the IMGUI launcher's click handler did before the surface moved onto the
/// game's own controls (ticket online-ui-art-and-controls-overhaul, S2a).
/// </summary>
public enum OnlineUiIntentKind
{
	/// <summary>The launcher was clicked: toggle the Online UI window.</summary>
	LauncherToggled,

	/// <summary>The pointer entered the launcher's rect — activity for the idle fade.</summary>
	LauncherHoverEntered,

	/// <summary>The pointer left the launcher's rect.</summary>
	LauncherHoverLeft,
}
