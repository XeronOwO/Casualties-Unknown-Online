namespace CasualtiesUnknownOnline.Runtime.Session.EntitySync;

/// <summary>
/// What one carry diagnostic window has to report, decided in the Runtime so the
/// decision is machine-checkable without a game and the adapter only renders it.
///
/// The three members are the three ways a carried rider's presentation can be
/// wrong in a way a log can see, and each is a READING that must be zero:
/// </summary>
/// <param name="LimbSeparation">
/// An exact limb pose was left behind the body root the ride pose pinned, by more
/// than <see cref="CarryPresentationReading.ReportThreshold"/> world units.
/// </param>
/// <param name="RiderDrift">
/// The clone was RENDERED away from the position a carry pin wrote for it, by more
/// than the threshold. Only a window with at least one pin in it can report this:
/// without a pin there was no placement to survive, which is
/// <see cref="NoCarryPin"/> instead of a drift.
/// </param>
/// <param name="NoCarryPin">
/// A live carry relation and not one carry pin in the whole window, so no frame of
/// it was pinned to a carrier at all. This is the structural fix not being engaged,
/// and it is a fact about the PIN only — a third-party view deliberately mounts
/// nothing, so "not mounted" is not this state.
/// </param>
public readonly record struct CarryAnomalies(bool LimbSeparation, bool RiderDrift, bool NoCarryPin)
{
	/// <summary>Nothing to report: the window has the shape a working carry presentation produces.</summary>
	public static CarryAnomalies None => default;

	/// <summary>Whether any anomaly is present.</summary>
	public bool Any => LimbSeparation || RiderDrift || NoCarryPin;
}
