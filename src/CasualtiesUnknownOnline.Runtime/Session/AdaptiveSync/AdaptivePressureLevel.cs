namespace CasualtiesUnknownOnline.Runtime.Session.AdaptiveSync;

/// <summary>
/// Coarse per-peer network pressure level derived from the existing ping/pong
/// peer-health telemetry. It is deliberately simple in Stage 1; later stages
/// can feed richer bandwidth/queue estimates into the same classification.
/// </summary>
public enum AdaptivePressureLevel
{
	/// <summary>No pressure observed; streams run at their configured default.</summary>
	Optimal,

	/// <summary>Mild degradation is appropriate.</summary>
	Moderate,

	/// <summary>Strong degradation is appropriate.</summary>
	High,

	/// <summary>Only the minimum viable cadence should remain.</summary>
	Critical,
}
