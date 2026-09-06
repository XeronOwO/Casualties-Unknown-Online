namespace CasualtiesUnknownOnline.Runtime.Session.AdaptiveSync;

/// <summary>
/// Logical adaptive-stream identifiers for frequent loss-tolerant streams.
/// Reliable control paths are intentionally not enumerated here because they
/// are outside adaptive throttling. Keeping ids typed avoids string-keyed
/// registries and lets the rate policy/catalog own the per-stream semantics.
/// </summary>
public enum AdaptiveStreamId
{
	/// <summary>Host → guests: the 20 Hz convergent player state fan-out.</summary>
	PlayerStateBroadcast,

	/// <summary>Guest → host: the 20 Hz local player state report.</summary>
	PlayerStateReport,

	/// <summary>Host → guests: the 20 Hz host-authoritative enemy presentation stream.</summary>
	EnemyStateBroadcast,

	/// <summary>Host → guests: the 20 Hz tutorial-claw presentation stream.</summary>
	TutorialClawBroadcast,

	/// <summary>Guest → host: medical injection frame-level deltas (cumulative, reliably coalesced).</summary>
	MedicalInjectionReport,

	/// <summary>Guest → host: shrapnel ordinary held-piece position reports (latest per piece, unreliable).</summary>
	ShrapnelPositionReport,
}
