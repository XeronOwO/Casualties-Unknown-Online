namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// Where a panel of the Online UI sits on CUO's surface (ticket online-ui-art-and-controls-overhaul, S5).
/// Both anchors are geometry and nothing else: the surface's own rectangle is the screen, so the Runtime
/// can say where a panel belongs without knowing a pixel of it.
/// </summary>
public enum OnlineUiPanelAnchor
{
	/// <summary>The panel hangs in the canvas's bottom-right corner, a margin in — the quick panel's rect
	/// in the IMGUI pass, kept.</summary>
	BottomRight,

	/// <summary>The panel's top-left corner is placed from the model's own screen point (the player context
	/// menu, which opens where the player right-clicked).</summary>
	Point,
}
