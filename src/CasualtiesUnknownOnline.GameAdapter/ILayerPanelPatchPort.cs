namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The end-of-layer panel's close, asked in the game's own write frame. <c>WorldGeneration.Update</c> opens the panel
/// with a latch that tests only <c>!savePanel.activeSelf</c> and the LOCAL body's height, so a close written from any
/// later phase is undone on the next frame. Closing right after that write is what makes the rule stick (read on the
/// machine, 2026-10-24) — and the rule is about the whole PANEL: the host shows it only when the living share reached
/// the host rule, and a member never shows it at all.
/// </summary>
internal interface ILayerPanelPatchPort
{
	/// <summary>Close the panel when this client must not show it: a member always, the host unless the share is met.</summary>
	void CloseTheEndOfLayerPanelIfThisClientMustNotShowIt();
}
