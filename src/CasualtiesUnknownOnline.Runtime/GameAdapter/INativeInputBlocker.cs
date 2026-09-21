using System.Collections.Generic;

namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// How the CUO Online UI tells the adapter which native input to suppress. The
/// game's own menu/world input is read by UGUI raycasts and custom button
/// inputs, and IMGUI does not participate in that input path, so the adapter has
/// to be told which CUO surfaces cover the screen.
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
	/// Sets the screen-space rectangles occupied by non-modal CUO Online UI
	/// surfaces (quick panel, right-click context menu). The adapter adds
	/// transparent UGUI raycast blockers that only intercept pointer input
	/// inside those rectangles, so clicks outside a small panel still reach the
	/// game world/menu. Pass an empty list to clear them.
	/// </summary>
	void SetOnlineUiScopedBlocks(IReadOnlyList<OnlineUiBlockRect> blocks);

	/// <summary>
	/// Tells the Game Adapter whether a non-modal CUO surface that closes on ESC
	/// (currently the quick panel) is visible. It is used only to suppress the
	/// native pause toggle while such a surface is open, without making the
	/// surface fully modal.
	/// </summary>
	void SetOnlineUiEscapeSurfaceVisible(bool visible);
}
