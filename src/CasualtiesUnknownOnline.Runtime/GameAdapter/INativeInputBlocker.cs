namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// How the CUO Online UI tells the adapter which native input to suppress. The
/// game's own menu/world input is read by UGUI raycasts and custom button
/// inputs, so the adapter has to be told which CUO surfaces own the frame.
///
/// <para>
/// Only the MODAL fact is left here (ticket online-ui-art-and-controls-overhaul, S5): every CUO surface
/// is a uGUI control on the game's own canvas now, so the non-modal ones — the quick panel and the
/// in-world player context menu — block their own pixels through the game's own raycasts, and the
/// rectangle-list member the IMGUI era needed (<c>SetOnlineUiScopedBlocks</c>, with its
/// <c>OnlineUiBlockRect</c> value and the adapter's <c>OnlineScopedRaycastFilter</c>) retired with the
/// panels that were its only consumers.
/// </para>
/// </summary>
public interface INativeInputBlocker
{
	/// <summary>
	/// Tells the Game Adapter whether the CUO Online UI modal window is open.
	/// While open, the adapter suppresses interaction with the game's
	/// background UI (custom AdaptiveButton inputs and UGUI raycasts) so clicks
	/// on the UI's non-control areas do not leak to the menu/world behind it.
	/// </summary>
	void SetOnlineUiModal(bool visible);

	/// <summary>
	/// Tells the Game Adapter whether a non-modal CUO surface that closes on ESC
	/// (currently the quick panel) is visible. It is used only to suppress the
	/// native pause toggle while such a surface is open, without making the
	/// surface fully modal.
	/// </summary>
	void SetOnlineUiEscapeSurfaceVisible(bool visible);
}
