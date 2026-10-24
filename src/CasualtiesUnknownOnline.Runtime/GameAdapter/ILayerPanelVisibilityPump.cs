namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// The end-of-layer panel's late-frame pass. The host shell calls it from Unity's <c>LateUpdate</c> — the only phase
/// that runs after every game and CUO <c>Update</c> and still before the frame renders — because the panel's
/// visibility is decided by more than the native local-body test: the host rule needs the share of LIVING players at
/// the floor, and the game's own <c>WorldGeneration.Update</c> writes the same object every frame. Writing from
/// <c>Update</c> lost that race on the machine (the panel stayed hidden with both living bodies at the floor, read
/// 2026-10-24), so the write belongs to the last phase.
/// </summary>
public interface ILayerPanelVisibilityPump
{
	/// <summary>
	/// Shows or hides <c>WorldGeneration.savePanel</c> for the frame about to render: on the host in an active session
	/// by the living group's share, and nowhere else.
	/// </summary>
	void PumpLayerPanelVisibility();
}
