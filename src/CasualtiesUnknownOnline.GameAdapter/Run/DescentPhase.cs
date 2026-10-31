namespace CasualtiesUnknownOnline.GameAdapter.Run;

/// <summary>
/// How far the host-descent follow got — the piece the run phase does not carry (the phase says what the CLIENT shows, this says how far the descent's own
/// generation went).
/// </summary>
internal enum DescentPhase
{
	/// <summary>Not following a host descent.</summary>
	None,

	/// <summary>The edge was taken; the local regeneration has not launched yet (the baseline hold).</summary>
	Accepted,

	/// <summary>The generation actually started (the gen wrapper left its hold).</summary>
	Launched,

	/// <summary>The host released the gate while this generation was still running.</summary>
	ReleasedEarly,

	/// <summary>The generation settled — waiting for the host's release.</summary>
	WaitingReady,
}
