namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>The per-item follow decision for one frame (PURE data — the
/// GameAdapter executes the writes; the decision carries the target values
/// it needs).</summary>
internal readonly struct FollowDecision
{
	/// <summary>Frozen (no stream tick yet — never pumped), Settled (ease the
	/// residual gap away), Moving (local physics runs from the host's velocity).</summary>
	internal FollowMode Mode { get; init; }

	/// <summary>Moving mode and the divergence exceeds the snap threshold — hard-snap the copy
	/// to the host's state (position + rotation + velocity, the local inertia is discarded).</summary>
	internal bool HardSnap { get; init; }

	/// <summary>Settled mode and the residual gap exceeds the settle threshold — ease toward the host's spot.</summary>
	internal bool EaseToTarget { get; init; }

	/// <summary>EaseToTarget: the Lerp coefficient for this frame (clamp01(deltaTime × rate)).</summary>
	internal float EaseK { get; init; }

	/// <summary>Settled mode and the gap exceeds the diagnostic threshold — worth a log line.</summary>
	internal bool LogDivergence { get; init; }

	/// <summary>The divergence this decision was computed from (the adapter's log lines).</summary>
	internal float Dist { get; init; }

	internal float TargetX { get; init; }
	internal float TargetY { get; init; }
	internal float TargetRot { get; init; }
	internal float VelX { get; init; }
	internal float VelY { get; init; }
	internal float AngVel { get; init; }
}
