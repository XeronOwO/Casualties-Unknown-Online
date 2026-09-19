using CasualtiesUnknownOnline.Runtime.Session.World;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The host-side entry-repair cadence in isolation (the sibling of
/// <c>CarriedInventoryReportScheduleTests</c>): the first repeat of an entry always repairs, the
/// cadence then suppresses claims until its interval has passed, and a clock that went backwards
/// (Environment.TickCount wraps) counts as elapsed instead of stalling the repair for the rest of
/// the wrap. The window's own arithmetic — the guest re-asserts every 5 s for 60 s, so a window
/// can be answered six times — is asserted in <c>SyncCadenceDecisionTests</c>, and the behaviour
/// these constants produce end to end is in <c>EntryRepairConvergenceTests</c>.
/// </summary>
public class EntryRepairScheduleTests
{
	private const long Interval = EntryRepairSchedule.RepairIntervalMs;

	[Fact]
	public void FirstClaim_AlwaysRepairs()
	{
		var schedule = new EntryRepairSchedule();
		schedule.Arm();

		Assert.True(schedule.TryClaim(0), "the first repeat of an entry is repaired at once");
		Assert.Equal(1, schedule.Repairs);
	}

	[Fact]
	public void ClaimsInsideTheInterval_AreSuppressed()
	{
		var schedule = new EntryRepairSchedule();
		schedule.Arm();
		Assert.True(schedule.TryClaim(1_000));

		Assert.False(schedule.TryClaim(1_000 + Interval - 1), "the guest's 5 s repeats must not each buy a repair pass");
		Assert.Equal(1, schedule.Repairs);
	}

	[Fact]
	public void ClaimsAtOrAfterTheInterval_AreAnswered()
	{
		var schedule = new EntryRepairSchedule();
		schedule.Arm();
		Assert.True(schedule.TryClaim(1_000));

		Assert.True(schedule.TryClaim(1_000 + Interval), "a window that stays open keeps being answered");
		Assert.Equal(2, schedule.Repairs);
	}

	[Fact]
	public void AClockThatWentBackwards_CountsAsElapsed()
	{
		var schedule = new EntryRepairSchedule();
		schedule.Arm();
		Assert.True(schedule.TryClaim(600_000));

		Assert.True(schedule.TryClaim(1_000), "a wrapped tick count must re-base instead of suppressing the repair for the rest of the wrap");
		Assert.Equal(2, schedule.Repairs);
	}

	[Fact]
	public void Arm_StartsTheNextEntrysBudgetFromZero()
	{
		var schedule = new EntryRepairSchedule();
		schedule.Arm();
		Assert.True(schedule.TryClaim(1_000));
		Assert.False(schedule.TryClaim(1_001));

		schedule.Arm(); // a new entry (or a reconnect, which re-arms on its own path)

		Assert.True(schedule.TryClaim(1_002), "the previous entry's cooldown must not suppress the new entry's first repair");
		Assert.Equal(1, schedule.Repairs);
	}
}
