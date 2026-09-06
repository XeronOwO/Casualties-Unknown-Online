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

	/// <summary>Host → guests: the 10 Hz world-item movement stream (absolute overwrite, unreliable).</summary>
	WorldItemMoveStream,

	/// <summary>Host → guests: the periodic world-item full-table snapshot (absolute overwrite, unreliable).</summary>
	WorldItemSnapshotStream,

	/// <summary>Host → guests: the 10 Hz fluid changed-region diff stream (absolute RLE overwrite, unreliable).</summary>
	FluidRegionDiffStream,

	/// <summary>Host → guests: the 1 Hz fluid full-viewport reconciliation stream (absolute RLE overwrite, unreliable).</summary>
	FluidRegionFullStream,

	/// <summary>Host → guests: the trader-state fallback full-state broadcast (absolute overwrite, cadence-adapted only).</summary>
	TraderStateStream,
}
