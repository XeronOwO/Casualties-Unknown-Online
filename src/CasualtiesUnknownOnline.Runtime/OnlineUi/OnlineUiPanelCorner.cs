namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// A panel's top-left corner (origin bottom-left, Y up) in the space its caller asked in — what
/// <see cref="OnlineUiPanelPlacement"/> answers with (ticket online-ui-art-and-controls-overhaul, S5). The
/// surface asks in its own canvas units, so the corner is a canvas position, not a pixel one.
/// </summary>
public readonly record struct OnlineUiPanelCorner(float X, float Top);
