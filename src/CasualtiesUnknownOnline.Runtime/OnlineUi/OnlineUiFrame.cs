namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// What the Online UI's native surface must show for one frame: the launcher's caption and the opacity
/// the idle fade derived for it, plus every panel that is open (ticket online-ui-art-and-controls-overhaul
/// — S2a put the launcher on the game's own control, S2b the window family, S5 the last two IMGUI panels).
///
/// <para>
/// The frame is pushed once per update and the surface applies it to the game's own controls, which is
/// why both the alpha and the display lists travel as values instead of being derived on the Unity side:
/// the rules stay in the Runtime, where they are tested (<see cref="OnlineUiLauncherFade"/>,
/// <see cref="OnlineUiPanelPlacement"/>), and the surface only has to put them on the controls it owns. A
/// null member is "that surface is not shown": none of them is a second surface with a visibility of its
/// own.
/// </para>
/// </summary>
public readonly record struct OnlineUiFrame(
	string LauncherLabel,
	float LauncherAlpha,
	OnlineUiWindowModel? Window,
	OnlineUiPanelModel? QuickPanel,
	OnlineUiPanelModel? ContextMenu);
