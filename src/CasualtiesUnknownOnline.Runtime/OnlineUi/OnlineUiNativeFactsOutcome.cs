namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// How one probe run ended (<see cref="OnlineUiNativeFactsCapturePolicy"/>). The caller logs exactly
/// once, when the run leaves <see cref="Pending"/>, and the state says which of the two halves to
/// report: the reading, or the fact that the game's UI never became readable.
/// </summary>
public enum OnlineUiNativeFactsOutcome
{
	/// <summary>Still trying: the four unknowns are not all read and the budget is not spent.</summary>
	Pending,

	/// <summary>All four unknowns were read in one attempt.</summary>
	Complete,

	/// <summary>The game's canvas was found, but at least one unknown stayed unreadable until the deadline.</summary>
	Partial,

	/// <summary>No game canvas ever appeared — the probe ran before the game had one, or a game update moved it.</summary>
	SurfaceMissing,
}
