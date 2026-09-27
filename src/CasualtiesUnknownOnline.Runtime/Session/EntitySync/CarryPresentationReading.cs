using System;

namespace CasualtiesUnknownOnline.Runtime.Session.EntitySync;

/// <summary>
/// The decisions of the carry presentation's diagnostics: the one tolerance that
/// turns a measured reading into a report, the clone set whose line is written at
/// the default log level, and the pure arithmetic behind the two readings. They
/// live in the Runtime because they are the part a test can settle without a
/// game; the adapter captures the transforms and renders what these rules decide.
///
/// Two readings are taken on a carried rider clone, and the carry relation is
/// doing its job exactly when both are zero: <c>limbSeparation</c> — how far a
/// rendered exact limb pose was left behind the body root the ride pose pinned
/// (<see cref="CarriedLimbAnchor"/>) — and <c>riderDrift</c> — how far the clone
/// was RENDERED from the position the last carry pin wrote for it, relative to
/// its carrier. Either one being reportable is the anomaly the rider-teleport
/// ticket waits for, and it is the half of that question a log can answer; the
/// picture on the screen still needs eyes.
/// </summary>
public static class CarryPresentationReading
{
	/// <summary>
	/// The smallest reading worth a report, in world units — about half a percent
	/// of a body height, the scale at which a separation starts to be visible on
	/// the sprite. It is a deliberate TOLERANCE, not the arithmetic's noise floor:
	/// the transform arithmetic scatters by roughly 1e-5 here, two orders of
	/// magnitude below, so everything this threshold hides is invisible and
	/// nothing it hides is a defect. Readings below it are still PRINTED, zero
	/// included, so a quiet window and an unmeasured one stay distinguishable.
	/// </summary>
	public const float ReportThreshold = 0.005f;

	/// <summary>
	/// Whether a reading must be reported. Stated as "not a small non-negative
	/// number" so the values that mean the MEASUREMENT itself failed — a
	/// negative distance, or not-a-number — are reported rather than silently
	/// read as the expected zero.
	/// </summary>
	public static bool IsReportable(float reading) => !(reading >= 0f && reading <= ReportThreshold);

	/// <summary>
	/// Whether one clone's 1 Hz diagnostic is a CARRY PARTICIPANT's line — the one
	/// family whose presentation is under an open defect, and therefore the one
	/// whose line is written at the default log level while every other clone
	/// keeps the routine Debug position line. All five facts are facts the
	/// diagnostic already has: the local player's own relation tags, the two
	/// driver flags a third-party view sets, and whether any carry pin was written
	/// during the window.
	/// </summary>
	public static bool IsCarryParticipant(
		bool isLocalRiderClone,
		bool isLocalCarrierClone,
		bool isRemoteRider,
		bool isRemoteCarrier,
		bool pinnedInWindow) =>
		isLocalRiderClone || isLocalCarrierClone || isRemoteRider || isRemoteCarrier || pinnedInWindow;

	/// <summary>
	/// What the diagnostics of one window must report, from the facts the adapter
	/// observed.
	/// </summary>
	/// <param name="pinnedInWindow">Whether a carry pin was written during the window at all.</param>
	/// <param name="limbSeparation">The window's largest limb-to-pinned-root reading.</param>
	/// <param name="riderDrift">The window's largest rendered-to-pin reading.</param>
	/// <param name="carriedRider">Whether the clone is the rider of a live carry relation at the diagnostic tick.</param>
	public static CarryAnomalies Anomalies(
		bool pinnedInWindow,
		float limbSeparation,
		float riderDrift,
		bool carriedRider) =>
		new(
			LimbSeparation: IsReportable(limbSeparation),
			RiderDrift: pinnedInWindow && IsReportable(riderDrift),
			NoCarryPin: !pinnedInWindow && carriedRider);

	/// <summary>
	/// The rider-minus-anchor offset one carry pin stores: where the rider was
	/// placed, measured against the carrier it was placed on. It is invariant
	/// while the rider actually rides that carrier, which is what makes the
	/// comparison below immune to the pair simply moving.
	/// </summary>
	public static void Offset(
		float riderX,
		float riderY,
		float anchorX,
		float anchorY,
		out float offsetX,
		out float offsetY)
	{
		offsetX = riderX - anchorX;
		offsetY = riderY - anchorY;
	}

	/// <summary>
	/// How far the rider sits from the position the stored offset implies, once
	/// both are measured against the SAME anchor. This is the whole reading, and
	/// it is relative on purpose: a carrier that walked, turned, crouched or was
	/// interpolated by the renderer moved the rider with it, so the offset is
	/// unchanged and the reading stays zero; only a rider that did not travel with
	/// its carrier — or an offset that belongs to an earlier frame, which is the
	/// same failure seen from the other side — shows up as a distance.
	///
	/// Its reach, which the acceptance run must not overread: a carrier moved AFTER
	/// its own last pin takes a mounted rider with it through the transform
	/// hierarchy, so both sides stay stale together and that frame reads zero by
	/// construction. The reading answers "did the pin's placement survive to the
	/// frame that rendered", never "was the pair drawn together".
	/// </summary>
	public static float Drift(
		float riderX,
		float riderY,
		float anchorX,
		float anchorY,
		float offsetX,
		float offsetY)
	{
		var deltaX = (riderX - anchorX) - offsetX;
		var deltaY = (riderY - anchorY) - offsetY;
		return (float)Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
	}
}
