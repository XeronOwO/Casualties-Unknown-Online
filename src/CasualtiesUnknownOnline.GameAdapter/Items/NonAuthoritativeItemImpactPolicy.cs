namespace CasualtiesUnknownOnline.GameAdapter.Items;

/// <summary>
/// Decides whether a native item impact presentation belongs on this side.
/// In a live guest session the host owns world-item physics; guest copies are
/// non-authoritative local simulations. Their native collision effects (drop,
/// step, plush squeak, impact dust) would otherwise play as if the local copy
/// were the physics authority, and background/frame-rate changes can make
/// those effects appear or disappear independently of the host's state.
/// Host/solo copies keep the original native behaviour.
/// </summary>
internal static class NonAuthoritativeItemImpactPolicy
{
	internal static bool ShouldSuppress(bool isSessionActive, bool isHostMode, bool isStandaloneWorldItem) =>
		isSessionActive && !isHostMode && isStandaloneWorldItem;

	/// <summary>
	/// The reporting half of the same rule: the side that still SIMULATES the
	/// landing keeps the native presentation and reports it, so every guest can
	/// present the same impact. Host-side, live session, standalone world item —
	/// exactly the case <see cref="ShouldSuppress"/> takes away from a guest, so
	/// the two halves can never both be true nor both be false for one impact.
	/// </summary>
	internal static bool ShouldReport(bool isSessionActive, bool isHostMode, bool isStandaloneWorldItem) =>
		isSessionActive && isHostMode && isStandaloneWorldItem;
}
