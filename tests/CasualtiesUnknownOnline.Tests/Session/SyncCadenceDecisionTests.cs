using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.AdaptiveSync;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The cadence review (<c>review/sync-cadence-review.md</c>) as executable decisions.
/// Every number here is computed from the PRODUCTION catalog and policy — never from a
/// copy of the arithmetic — so the worst-case divergence the matrix records cannot drift
/// away from the code that produces it, and changing a cap has to face this file.
///
/// <para>
/// The decisions: the world-item keyframe may stretch to 10 s (was 30 s), the trader
/// fallback to 15 s (was 30 s), the fluid full-viewport reconciliation keeps its 1 s base
/// and its 10 s cap (which pressure never reaches: 1 s / 0.15 ≈ 6.7 s is the worst case),
/// the entry group's first resend rides the guest's own 5 s window instead of the host's
/// 60 s cycle, and the carried-inventory registration converges within one 5 s burst step.
/// The 60 s steady cycles stay as the durable fallback behind all of them.
/// </para>
/// </summary>
public class SyncCadenceDecisionTests
{
	private readonly AdaptiveRatePolicy _policy = new();

	[Fact]
	public void ItemKeyframe_WorstCaseDivergence_IsCappedAtTenSeconds()
	{
		var profile = Profile(AdaptiveStreamId.WorldItemSnapshotStream);

		Assert.Equal(5_000, IntervalAt(profile, AdaptivePressureLevel.Optimal));
		Assert.Equal(8_333, IntervalAt(profile, AdaptivePressureLevel.Moderate));
		Assert.Equal(10_000, IntervalAt(profile, AdaptivePressureLevel.High)); // the cap already binds at High (5 000 / 0.35 = 14 286)
		Assert.Equal(10_000, IntervalAt(profile, AdaptivePressureLevel.Critical));
		Assert.Equal(10_000, profile.MaxIntervalMs);
		Assert.True(IntervalAt(profile, AdaptivePressureLevel.High) <= profile.MaxIntervalMs,
			"the cap is the worst case at every pressure level: the keyframe is the only absolute heal for item condition/liquids/components and for phantom-item removal");
	}

	[Fact]
	public void TraderFallback_WorstCaseDivergence_IsCappedAtFifteenSeconds()
	{
		var profile = Profile(AdaptiveStreamId.TraderStateStream);

		Assert.Equal(5_000, IntervalAt(profile, AdaptivePressureLevel.Optimal));
		Assert.Equal(8_333, IntervalAt(profile, AdaptivePressureLevel.Moderate));
		Assert.Equal(14_286, IntervalAt(profile, AdaptivePressureLevel.High));
		Assert.Equal(15_000, IntervalAt(profile, AdaptivePressureLevel.Critical));
		Assert.Equal(15_000, profile.MaxIntervalMs);
		Assert.True(IntervalAt(profile, AdaptivePressureLevel.High) <= profile.MaxIntervalMs,
			"the fallback only heals a swallowed interaction broadcast: it may take longer than the keyframe, never longer than the cap");
	}

	[Fact]
	public void FluidFullViewport_WorstCaseDivergence_IsUnderItsCap()
	{
		var profile = Profile(AdaptiveStreamId.FluidRegionFullStream);
		var worst = IntervalAt(profile, AdaptivePressureLevel.Critical);

		Assert.Equal(1_000, IntervalAt(profile, AdaptivePressureLevel.Optimal));
		Assert.Equal(1_667, IntervalAt(profile, AdaptivePressureLevel.Moderate));
		Assert.Equal(2_857, IntervalAt(profile, AdaptivePressureLevel.High));
		Assert.Equal(6_667, worst); // 1 000 ms / the priority-2 Critical factor 0.15
		Assert.True(worst < profile.MaxIntervalMs,
			$"the 10 s cap never binds — pressure alone reaches {worst} ms, so the accepted worst case is the measured value and not the cap");
	}

	[Fact]
	public void EntryGroup_FirstResend_ArrivesInsideTheGuestWindow()
	{
		Assert.Equal(5_000, SessionControlConvergence.IntervalMs);
		Assert.Equal(60_000, SessionControlConvergence.IntervalMs * SessionControlConvergence.MaxReports);

		var windowMs = SessionControlConvergence.IntervalMs * SessionControlConvergence.MaxReports;
		Assert.True(EntryRepairSchedule.RepairIntervalMs < windowMs,
			"the repair cadence must fit inside the guest's own window, or a still-open window would stop being answered before the window itself gives up");
		Assert.Equal(6, windowMs / EntryRepairSchedule.RepairIntervalMs);
	}

	[Fact]
	public void CarriedInventoryRegistration_ConvergesWithinOneBurstStep()
	{
		var schedule = new CarriedInventoryReportSchedule();
		schedule.Arm(0);

		Assert.True(schedule.IsDue(0), "the registration edge reports at once");
		schedule.MarkReported(0);

		Assert.False(schedule.IsDue(CarriedInventoryReportSchedule.BurstIntervalMs - 1));
		Assert.True(schedule.IsDue(CarriedInventoryReportSchedule.BurstIntervalMs),
			"a swallowed first report converges on the next burst step — the 5 s dense cadence is the first-resend latency");
		Assert.Equal(60_000, CarriedInventoryReportSchedule.BurstIntervalMs * CarriedInventoryReportSchedule.BurstReports);
		Assert.Equal(60_000, CarriedInventoryReportSchedule.SteadyIntervalMs);
	}

	private static AdaptiveStreamProfile Profile(AdaptiveStreamId id)
	{
		Assert.True(AdaptiveStreamCatalog.TryGet(id, out var profile), $"the adaptive catalog must declare {id}");

		return profile;
	}

	private long IntervalAt(AdaptiveStreamProfile profile, AdaptivePressureLevel pressure) =>
		_policy.GetEffectiveIntervalMs(profile, pressure, profile.BaseIntervalMs);
}
