namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// One fact the Online UI's native surface reports back, in the order it happened. The surface owns no
/// semantics: a click is "this control was clicked", not "the window is now open" — the plugin decides
/// what that means, exactly as the IMGUI controls' inline handlers did before the surfaces moved onto
/// the game's own controls (ticket online-ui-art-and-controls-overhaul, S2a for the launcher, S2b for
/// the window family).
/// </summary>
public enum OnlineUiIntentKind
{
	/// <summary>The launcher was clicked: toggle the Online UI window.</summary>
	LauncherToggled,

	/// <summary>The pointer entered the launcher's rect — activity for the idle fade.</summary>
	LauncherHoverEntered,

	/// <summary>The pointer left the launcher's rect.</summary>
	LauncherHoverLeft,

	/// <summary>The pointer entered the WINDOW's rect. The window's own rect is the surface's fact now,
	/// and the plugin still has to know it: an in-world right-click inside the window belongs to the UI,
	/// not to the world.</summary>
	WindowHoverEntered,

	/// <summary>The pointer left the window's rect.</summary>
	WindowHoverLeft,

	/// <summary>A button was clicked — a page control or a tab (<see cref="OnlineUiIntent.ControlId"/>).</summary>
	ControlInvoked,

	/// <summary>A checkbox flipped; <see cref="OnlineUiIntent.Flag"/> is the value it now holds.</summary>
	ControlToggled,

	/// <summary>A dropdown option was selected; <see cref="OnlineUiIntent.Index"/> is the option's index.</summary>
	ControlSelected,

	/// <summary>A slider moved; <see cref="OnlineUiIntent.Number"/> is the value it now holds.</summary>
	ControlChanged,

	/// <summary>A text field's contents changed; <see cref="OnlineUiIntent.Text"/> is what it now holds.</summary>
	ControlEdited,
}
