using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// One CUO panel for one frame (ticket online-ui-art-and-controls-overhaul, S5): the quick panel, and the
/// in-world player context menu. A plain value like every other model that crosses the adapter boundary —
/// the plugin builds it from the runtime facts and the action delegates it already reads, and the adapter
/// puts it on the game's own controls.
///
/// <para>
/// It is the window's model without the window: no tabs, no title bar to drag, no scroll — a title, an
/// optional close control and rows, which is what both remaining IMGUI panels were. The rows are the same
/// <see cref="OnlineUiRowModel"/> the pages are built from, so the two panels render through the same
/// control views and the same wrapping rule as the window, and the only thing this type adds is where the
/// panel hangs.
/// </para>
///
/// <para>
/// <see cref="X"/> and <see cref="Y"/> are SCREEN coordinates (origin bottom-left, Y up), the space the
/// surface's own canvas lives in; they are read only for <see cref="OnlineUiPanelAnchor.Point"/>, and the
/// plugin has already clamped the corner into the screen by then
/// (<see cref="OnlineUiPanelPlacement"/>), because where a panel may sit is a rule and not a pixel fact.
/// <see cref="Width"/> is the panel's fixed width in the canvas's own units; its height is the content's.
/// A <see cref="CloseId"/> of "" means the panel has no close control of its own (the context menu closes
/// on a pick, on a click away and never had one).
/// </para>
/// </summary>
public sealed record OnlineUiPanelModel(
	string Title,
	string CloseId,
	OnlineUiPanelAnchor Anchor,
	float X,
	float Y,
	float Width,
	IReadOnlyList<OnlineUiRowModel> Rows)
{
	/// <summary>The quick panel's shape: docked in the canvas's bottom-right corner, with a close control
	/// carrying <paramref name="closeId"/>.</summary>
	public static OnlineUiPanelModel Docked(
		string title,
		string closeId,
		float width,
		IReadOnlyList<OnlineUiRowModel> rows) =>
		new(title, closeId, OnlineUiPanelAnchor.BottomRight, 0f, 0f, width, rows);

	/// <summary>The context menu's shape: its top-left corner from the screen point the player clicked,
	/// clamped by <see cref="OnlineUiPanelPlacement"/> before it gets here, and no close control.</summary>
	public static OnlineUiPanelModel AtPoint(
		string title,
		float screenX,
		float screenY,
		float width,
		IReadOnlyList<OnlineUiRowModel> rows) =>
		new(title, "", OnlineUiPanelAnchor.Point, screenX, screenY, width, rows);
}
