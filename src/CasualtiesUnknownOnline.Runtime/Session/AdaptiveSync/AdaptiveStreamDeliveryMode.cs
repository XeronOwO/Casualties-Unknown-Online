namespace CasualtiesUnknownOnline.Runtime.Session.AdaptiveSync;

/// <summary>
/// How a logical stream tolerates skipped/coalesced intermediate frames.
/// This is the explicit reliable/unreliable split the flow-control framework
/// relies on: only loss-tolerant streams may be rate-adapted; reliable control
/// paths are never throttled or dropped by this framework.
/// </summary>
public enum AdaptiveStreamDeliveryMode
{
	/// <summary>
	/// Reliable one-shot/control traffic. Must always be delivered; never
	/// adaptive, never coalesced/dropped.
	/// </summary>
	ReliableControl,

	/// <summary>
	/// Unreliable overwrite stream. Each frame is an absolute new value; a
	/// skipped intermediate frame is harmless because the next frame overwrites
	/// it. Example: player/enemy position streams.
	/// </summary>
	LatestWins,

	/// <summary>
	/// Unreliable cumulative stream. Frames carry deltas/progress; intermediate
	/// frames may be coalesced/lightened only if the domain has a terminal or
	/// full-state reconciliation. Example: medical frame-level injection deltas.
	/// </summary>
	Cumulative,
}
