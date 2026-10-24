namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The end-of-layer panel's close, asked in the game's own write frame. <c>WorldGeneration.Update</c> opens the panel
/// with a latch that tests only <c>!savePanel.activeSelf</c> and the LOCAL body's height, so a close written from any
/// later phase is undone on the next frame. Closing right after that write is what makes "the share has not been met"
/// stick (read on the machine, 2026-10-24).
/// </summary>
internal interface ILayerPanelPatchPort
{
	/// <summary>Close the panel when the living group's share has not reached the host rule; a no-op off the host.</summary>
	void CloseTheLayerPanelIfTheShareIsNotMet();
}
