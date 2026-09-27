using System;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The decisions behind the carried rider's two readings, where a test can settle
/// them without a game. A carried rider clone has two readings —
/// <c>limbSeparation</c> (an exact limb pose left behind the body root the ride
/// pose pinned) and <c>riderDrift</c> (the clone rendered away from the position a
/// carry pin wrote for it) — and both are zero when the carry relation does its
/// job. The threshold is what keeps that statement usable: the distances come out
/// of different transform arithmetic, so a limb that DID travel with its root still
/// lands a few 1e-5 away, and a rule that reported those would bury the one reading
/// that matters. The drift arithmetic is here for the same reason: whether the
/// comparison is relative decides whether an ordinary walk reads as an anomaly, and
/// that is arithmetic, not an observation.
/// </summary>
public class CarryPresentationReadingTests
{
	[Fact]
	public void ExpectedZeroReading_IsNotReported() =>
		Assert.False(
			CarryPresentationReading.IsReportable(0f),
			"zero is the reading a correct carry presentation produces");

	[Theory]
	[InlineData(0.0001f)]
	[InlineData(0.001f)]
	[InlineData(0.005f)]
	public void ReadingInsideTheTolerance_IsNotReported(float reading) =>
		Assert.False(
			CarryPresentationReading.IsReportable(reading),
			"a distance below the tolerance is invisible on the sprite and is the arithmetic's own scatter, not evidence of anything");

	[Theory]
	[InlineData(0.006f)]
	[InlineData(0.02f)]
	[InlineData(0.35f)]
	[InlineData(3f)]
	public void ReadingAboveTheTolerance_IsReported(float reading) =>
		Assert.True(
			CarryPresentationReading.IsReportable(reading),
			"a reading above the tolerance is the defect being live on that screen, which is exactly what the acceptance run needs to see");

	[Theory]
	[InlineData(float.NaN)]
	[InlineData(-1f)]
	public void BrokenMeasurement_IsReported(float reading) =>
		Assert.True(
			CarryPresentationReading.IsReportable(reading),
			"a reading that is not a small non-negative number means the measurement itself failed, so the rule fails closed and reports it");

	[Fact]
	public void Tolerance_StaysBelowWhatAPlayerCouldSee() =>
		Assert.True(
			CarryPresentationReading.ReportThreshold < 0.05f,
			"the threshold may only hide the invisible: a visible separation must always be reported, so raising it is a deliberate change to what this rule means");

	[Theory]
	[InlineData(true, false, false, false, false)]
	[InlineData(false, true, false, false, false)]
	[InlineData(false, false, true, false, false)]
	[InlineData(false, false, false, true, false)]
	[InlineData(false, false, false, false, true)]
	public void AnyCarryFact_MakesTheCloneAParticipant(
		bool localRider, bool localCarrier, bool remoteRider, bool remoteCarrier, bool pinnedInWindow) =>
		Assert.True(
			CarryPresentationReading.IsCarryParticipant(localRider, localCarrier, remoteRider, remoteCarrier, pinnedInWindow),
			"a clone that participates in a carry relation is the family whose line a default session must show");

	[Fact]
	public void AnOrdinaryClone_IsNotAParticipant() =>
		Assert.False(
			CarryPresentationReading.IsCarryParticipant(
				isLocalRiderClone: false,
				isLocalCarrierClone: false,
				isRemoteRider: false,
				isRemoteCarrier: false,
				pinnedInWindow: false),
			"every clone outside the carry family keeps the routine Debug position line, so the default log stays small");

	[Fact]
	public void AQuietPinnedWindow_ReportsNothing() =>
		Assert.False(
			CarryPresentationReading.Anomalies(
				pinnedInWindow: true,
				limbSeparation: 0f,
				riderDrift: 0f,
				carriedRider: true).Any,
			"a carried rider with a pin in force and both readings at zero is the expected window");

	[Fact]
	public void APinnedRiderIsNeverReportedAsUnpinned() =>
		Assert.False(
			CarryPresentationReading.Anomalies(
				pinnedInWindow: true,
				limbSeparation: 0f,
				riderDrift: 0f,
				carriedRider: true).NoCarryPin,
			"a third-party view deliberately mounts nothing, so 'not mounted' must never be read as 'no pin in force': one pinned frame in the window is enough");

	[Fact]
	public void ALiveRelationWithNoPinInTheWindow_IsReported() =>
		Assert.True(
			CarryPresentationReading.Anomalies(
				pinnedInWindow: false,
				limbSeparation: 0f,
				riderDrift: 0f,
				carriedRider: true).NoCarryPin,
			"a rider whose whole window had no pin at all was not measured against a carrier on any frame, and that is the structural fix not being engaged");

	[Fact]
	public void NoPinWithoutACarryRelation_IsNotReported() =>
		Assert.False(
			CarryPresentationReading.Anomalies(
				pinnedInWindow: false,
				limbSeparation: 0f,
				riderDrift: 0f,
				carriedRider: false).NoCarryPin,
			"an ordinary remote clone has no relation and no pin, which is not an anomaly");

	[Fact]
	public void ADriftWithoutAPinInTheWindow_CannotBeReported() =>
		Assert.False(
			CarryPresentationReading.Anomalies(
				pinnedInWindow: false,
				limbSeparation: 0f,
				riderDrift: 40f,
				carriedRider: true).RiderDrift,
			"without a pin there was no placement for the clone to survive, so the window reports the missing pin instead of a distance nobody measured");

	[Fact]
	public void EachReading_IsReportedOnItsOwn() =>
		Assert.True(
			CarryPresentationReading.Anomalies(
				pinnedInWindow: true,
				limbSeparation: 0.35f,
				riderDrift: 0f,
				carriedRider: true).LimbSeparation
			&& CarryPresentationReading.Anomalies(
				pinnedInWindow: true,
				limbSeparation: 0f,
				riderDrift: 0.35f,
				carriedRider: true).RiderDrift,
			"either reading being reportable is an anomaly on its own; neither needs the other to be wrong");

	[Fact]
	public void Offset_IsTheRiderMeasuredAgainstItsCarrier()
	{
		CarryPresentationReading.Offset(riderX: 10f, riderY: 5f, anchorX: 2f, anchorY: 3f, out var offsetX, out var offsetY);

		Assert.True(Math.Abs(offsetX - 8f) < 0.0001f, "the stored offset answers 'where was the rider relative to its carrier'");
		Assert.True(Math.Abs(offsetY - 2f) < 0.0001f, "the stored offset answers 'where was the rider relative to its carrier'");
	}

	[Fact]
	public void Drift_WhenNothingMoved_IsZero()
	{
		CarryPresentationReading.Offset(riderX: 10f, riderY: 5f, anchorX: 2f, anchorY: 3f, out var offsetX, out var offsetY);

		Assert.True(
			CarryPresentationReading.Drift(riderX: 10f, riderY: 5f, anchorX: 2f, anchorY: 3f, offsetX, offsetY) < 0.0001f,
			"the clone still sits where the pin put it");
	}

	[Fact]
	public void Drift_WhenThePairMovedTogether_IsZero()
	{
		// The claim the whole reading rests on: the carrier walked (and, in game,
		// also turned, crouched and was interpolated by the renderer) while the
		// rider travelled with it, so the offset is unchanged. A comparison that
		// dropped the anchor would report the carrier's own position as a defect.
		CarryPresentationReading.Offset(riderX: 10f, riderY: 5f, anchorX: 2f, anchorY: 3f, out var offsetX, out var offsetY);

		Assert.True(
			CarryPresentationReading.Drift(riderX: 110f, riderY: -2f, anchorX: 102f, anchorY: -4f, offsetX, offsetY) < 0.0001f,
			"the carrier moved by (100, -7) and the rider moved with it: both readings of a walk are zero");
	}

	[Fact]
	public void Drift_WhenOnlyTheRiderMoved_IsTheDistanceItMoved()
	{
		CarryPresentationReading.Offset(riderX: 10f, riderY: 5f, anchorX: 2f, anchorY: 3f, out var offsetX, out var offsetY);

		Assert.True(
			Math.Abs(CarryPresentationReading.Drift(riderX: 13f, riderY: 9f, anchorX: 2f, anchorY: 3f, offsetX, offsetY) - 5f) < 0.0001f,
			"the rider was moved by (3, 4) while its carrier stayed put, and the reading is that distance");
	}

	[Fact]
	public void Drift_WithAnOffsetFromThePreviousFrame_IsTheCarriersTravel()
	{
		// The stale-offset failure seen from the other side: frame N-1 placed the
		// rider at (5, 2) against an anchor at (2, 2), so the true offset is (3, 0),
		// and the carrier then travels (5, 0). An offset captured before this
		// frame's placement records the PREVIOUS rider position against the NEW
		// anchor — (5, 2) - (7, 2) = (-2, 0) — so a rider that never moved relative
		// to its carrier still reads as the carrier's travel. That is why the pin is
		// stored AFTER the ride pose wrote the root.
		CarryPresentationReading.Offset(riderX: 5f, riderY: 2f, anchorX: 7f, anchorY: 2f, out var staleOffsetX, out var staleOffsetY);

		Assert.True(
			Math.Abs(CarryPresentationReading.Drift(riderX: 10f, riderY: 2f, anchorX: 7f, anchorY: 2f, staleOffsetX, staleOffsetY) - 5f) < 0.0001f,
			"the carrier travelled (5, 0) between the two frames and the stale offset reports it");
	}
}
