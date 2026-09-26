namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// What the Online UI's native surface must show for one frame: the launcher's caption and the opacity
/// the idle fade derived for it. The frame is pushed once per update and the surface applies it to the
/// game's own control — which is why the alpha travels as a value instead of being re-derived on the
/// Unity side: the rule stays in the Runtime, where it is tested (<see cref="OnlineUiLauncherFade"/>),
/// and the surface only has to put it on the launcher.
/// </summary>
public readonly record struct OnlineUiFrame(string LauncherLabel, float LauncherAlpha);
