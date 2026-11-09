namespace CasualtiesUnknownOnline.GameState.Domains.World;

/// <summary>
/// How a run ended at the bottom layer. The game itself picks the branch when the host's body reaches the
/// exit (<c>WorldGeneration.cs:1205</c>): the merchant's ending when the host carries the scarf, else the
/// surface breach. The kind is recorded on the run so every client plays the host's branch instead of
/// resolving its own.
/// </summary>
public enum RunEnding : byte
{
	/// <summary>No ending yet — the run is still descending.</summary>
	None = 0,

	/// <summary>The surface breach cutscene (the ordinary exit).</summary>
	SurfaceBreach = 1,

	/// <summary>The merchant's ending (the scarf is carried).</summary>
	Merc = 2,
}
