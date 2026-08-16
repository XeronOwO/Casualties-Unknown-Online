namespace CasualtiesUnknownOnline.Runtime.Protocol;

/// <summary>
/// The multiplayer world-time speeds CUO synchronizes. Values deliberately
/// mirror the game's <c>PlayerCamera.SpeedType</c> ordering for Normal/Fast/
/// SuperFast/UnconsciousFast/DyingFast; Slowmo and Paused are local
/// presentation and never ride the wire.
/// </summary>
public enum WorldTimeSpeed : byte
{
	/// <summary>1× — the default and the only speed allowed while anyone is moving.</summary>
	Normal = 0,

	/// <summary>5× fast-forward (PlayerCamera.SpeedType.Fast).</summary>
	Fast = 1,

	/// <summary>20× fast-forward (PlayerCamera.SpeedType.SuperFast).</summary>
	SuperFast = 2,

	/// <summary>25× sleep acceleration (PlayerCamera.SpeedType.UnconsciousFast).</summary>
	UnconsciousFast = 3,

	/// <summary>3.5× dying acceleration (PlayerCamera.SpeedType.DyingFast).</summary>
	DyingFast = 4,
}
