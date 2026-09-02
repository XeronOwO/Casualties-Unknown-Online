namespace CasualtiesUnknownOnline.Abstractions;

/// <summary>
/// Stable sleep-quality names for <see cref="ModTileDefinition.SleepQuality"/>.
/// The values are plain enums so Abstractions stays free of game types; the
/// Game Adapter maps them to the vanilla <c>Body.SleepQuality</c> enum.
/// </summary>
public enum ModTileSleepQuality
{
	/// <summary>The worst rest quality (poor ground).</summary>
	Bad = 0,

	/// <summary>An intermediate rest quality.</summary>
	Mediocre = 1,

	/// <summary>An above-average rest quality.</summary>
	Okay = 2,

	/// <summary>The best rest quality.</summary>
	Good = 3
}
