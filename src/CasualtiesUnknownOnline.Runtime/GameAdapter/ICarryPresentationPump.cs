namespace CasualtiesUnknownOnline.Runtime.GameAdapter;

/// <summary>
/// The carry presentation's late-frame pass. The host shell calls it from Unity's
/// <c>LateUpdate</c> — the only phase that runs after every game and CUO
/// <c>Update</c> and still before the frame renders — so a remote rider drawn on
/// the local carrier can never show the previous frame's carrier transform.
/// </summary>
public interface ICarryPresentationPump
{
	/// <summary>
	/// Re-pins every remote rider clone to the local carrier's final body transform
	/// for the frame about to render. A no-op while no local body exists.
	/// </summary>
	void PinCarriedPresentation();
}
