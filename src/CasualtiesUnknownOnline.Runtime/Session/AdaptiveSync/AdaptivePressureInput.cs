using CasualtiesUnknownOnline.Runtime.Session.NetworkTraffic;

namespace CasualtiesUnknownOnline.Runtime.Session.AdaptiveSync;

/// <summary>
/// The combined input for pressure classification: the existing peer-health
/// snapshot (RTT/jitter/probe loss) plus the new per-stream/per-peer traffic
/// estimate. A missing traffic observation is not evidence of pressure, so the
/// classifier falls back to health-only.
/// </summary>
internal readonly record struct AdaptivePressureInput(
	PeerHealthTracker.PeerHealthSnapshot? Health,
	AdaptiveTrafficEstimate? Traffic);
