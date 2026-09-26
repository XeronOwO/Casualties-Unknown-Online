namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// What the Online UI's native surface must show for one frame: the launcher's caption and the opacity
/// the idle fade derived for it, plus every surface that is open (ticket online-ui-art-and-controls-overhaul
/// — S2a put the launcher on the game's own control, S2b the window family, S5 the last two IMGUI panels,
/// S6 the world-space overlays).
///
/// <para>
/// The frame is pushed once per update and the surface applies it to the game's own controls, which is
/// why both the alpha and the display lists travel as values instead of being derived on the Unity side:
/// the rules stay in the Runtime, where they are tested (<see cref="OnlineUiLauncherFade"/>,
/// <see cref="OnlineUiPanelPlacement"/>), and the surface only has to put them on the controls it owns. A
/// null member is "that surface is not shown": none of them is a second surface with a visibility of its
/// own.
/// </para>
///
/// <para>
/// <see cref="World"/> is the one member that is not a surface of CUO's own UI but an overlay ON the
/// world — the nameplates, the off-screen arrows, the network readout and the location pings — and it is
/// the one the surface must place itself, because placing it means projecting world positions with the
/// live camera. It is never null: an overlay with nothing to show is
/// <see cref="OnlineUiWorldOverlay.None"/>, so the surface reads one shape and the plugin has one state to
/// push.
/// </para>
/// </summary>
public readonly record struct OnlineUiFrame(
	string LauncherLabel,
	float LauncherAlpha,
	OnlineUiWindowModel? Window,
	OnlineUiPanelModel? QuickPanel,
	OnlineUiPanelModel? ContextMenu,
	OnlineUiWorldOverlay World);
