namespace CasualtiesUnknownOnline.Runtime.Session.Items;

/// <summary>
/// The position-domain motion thresholds (PURE — no Unity): the settled
/// criterion shared by the host's stream throttle and the guest's follow
/// decision, the guest's snap/ease thresholds and the ease rate. Physics
/// quantities are inputs (velocity sqrMagnitude, |angular velocity|, distance)
/// — the GameAdapter computes them from the rigidbodies/transforms and feeds
/// them in. One definition of "settled" keeps the two sides' decisions from
/// drifting apart.
/// </summary>
internal static class ItemMotionState
{
	/// <summary>Velocity below this sqrMagnitude counts as at rest (ItemPositionAuthority's noise floor).</summary>
	internal const float SettledVelocitySqr = 0.01f;

	/// <summary>|angular velocity| below this counts as at rest (no spin).</summary>
	internal const float SettledAngularVelocity = 0.1f;

	/// <summary>Position divergence beyond this many units hard-snaps the guest's copy to the host's state (clearing local inertia).</summary>
	internal const float SnapDistance = 3f;

	/// <summary>A settled copy with a residual gap larger than this eases toward the host's spot — the final rest state must converge.</summary>
	internal const float SettleSnapDistance = 0.05f;

	/// <summary>The settled ease's convergence rate per second (the Lerp coefficient = clamp01(deltaTime × rate)).</summary>
	internal const float SettleEaseRate = 12f;

	/// <summary>A settled copy diverged more than this is worth a diagnostic line (a real divergence, not residual jitter).</summary>
	internal const float SettleLogDistance = 0.5f;

	/// <summary>The settled criterion: velocity below the noise floor AND no spin — used by
	/// the host's throttle (decides the 1 Hz re-align round) and the guest's follow (decides
	/// the ease-to-rest mode) alike.</summary>
	internal static bool IsSettled(float velocitySqrMagnitude, float angularVelocityAbs) =>
		velocitySqrMagnitude < SettledVelocitySqr && angularVelocityAbs < SettledAngularVelocity;
}
