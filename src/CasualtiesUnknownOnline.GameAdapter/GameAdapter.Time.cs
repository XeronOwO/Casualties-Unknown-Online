namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The world-time domain members of <see cref="GameAdapter"/> (partial split
/// at the 600-line gate): the deep module lives in
/// <see cref="World.WorldTimeSync"/>; this surface only owns the field and
/// the two IPatchBridge forwards the thin patches call.
/// </summary>
public sealed partial class GameAdapter
{
	private readonly World.WorldTimeSync _worldTimeSync;

	bool IPatchBridge.OnTimeScaleSetRequested(PlayerCamera.SpeedType speed, bool force) =>
		_worldTimeSync.OnTimeScaleSetRequested(speed, force);

	void IPatchBridge.OnLocalTimeScaleChanged(PlayerCamera.SpeedType speed) =>
		_worldTimeSync.OnLocalTimeScaleChanged(speed);
}
