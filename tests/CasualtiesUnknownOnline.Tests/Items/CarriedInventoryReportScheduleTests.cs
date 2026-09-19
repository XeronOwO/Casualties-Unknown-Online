using CasualtiesUnknownOnline.Runtime.Session.Items;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Items;

/// <summary>
/// The registration cadence itself (sync-coverage audit row I8): an absolute
/// report opens dense — one step every five seconds for the entry swallow
/// window — and then settles to the steady once-a-minute re-assertion that keeps
/// a registration durable for the rest of the run.
/// </summary>
public class CarriedInventoryReportScheduleTests
{
	private static long DenseEnd(long openedAtMs) =>
		openedAtMs + (CarriedInventoryReportSchedule.BurstReports * CarriedInventoryReportSchedule.BurstIntervalMs);

	[Fact]
	public void UnarmedSchedule_IsNeverDue()
	{
		var schedule = new CarriedInventoryReportSchedule();

		Assert.False(schedule.IsDue(0));
		Assert.False(schedule.IsDue(600_000));
	}

	[Fact]
	public void Arming_ReportsAtOnce_ThenEveryDenseInterval()
	{
		var schedule = new CarriedInventoryReportSchedule();
		schedule.Arm(1_000);

		Assert.True(schedule.IsDue(1_000), "an opened window is due at once — the first report is the generation edge's");
		Assert.True(schedule.IsDue(1_050), "still due until a report spends the step");

		schedule.MarkReported(1_050);

		Assert.False(schedule.IsDue(1_050 + CarriedInventoryReportSchedule.BurstIntervalMs - 1));
		Assert.True(schedule.IsDue(1_050 + CarriedInventoryReportSchedule.BurstIntervalMs));
	}

	[Fact]
	public void DenseWindow_SpendsEveryBurstReport_ThenSettlesToTheSteadyInterval()
	{
		var schedule = new CarriedInventoryReportSchedule();
		schedule.Arm(0);

		var reports = 0;
		var nowMs = 0L;
		var lastReportMs = 0L;
		// Drive the pump for the whole dense window plus a little more.
		for (; nowMs < DenseEnd(0) + 1; nowMs += 100)
		{
			if (!schedule.IsDue(nowMs))
			{
				continue;
			}

			reports++;
			lastReportMs = nowMs;
			schedule.MarkReported(nowMs);
		}

		Assert.True(reports == CarriedInventoryReportSchedule.BurstReports, $"the dense window reports once per burst step, got {reports}");
		Assert.True(schedule.BurstRemaining == 0, "the dense budget is spent");

		// The steady half: an idle minute, then the durable re-assertion.
		Assert.False(schedule.IsDue(lastReportMs + CarriedInventoryReportSchedule.SteadyIntervalMs - 1), "the steady cadence is not the dense one");
		Assert.True(schedule.IsDue(lastReportMs + CarriedInventoryReportSchedule.SteadyIntervalMs), "the steady re-assertion is due one minute later");
	}

	[Fact]
	public void ArmingAgain_ReopensTheDenseWindow()
	{
		var schedule = new CarriedInventoryReportSchedule();
		schedule.Arm(0);
		for (var i = 0; i < CarriedInventoryReportSchedule.BurstReports; i++)
		{
			schedule.MarkReported(i * CarriedInventoryReportSchedule.BurstIntervalMs);
		}

		Assert.True(schedule.BurstRemaining == 0, "the window is spent");

		// The host granted the id watermark (join/reconnect): the next report is due
		// at once and the dense cadence starts over.
		schedule.Arm(100_000);

		Assert.True(schedule.BurstRemaining == CarriedInventoryReportSchedule.BurstReports, "re-arming refills the dense budget");
		Assert.True(schedule.IsDue(100_000));
	}

	[Fact]
	public void Reset_ClosesTheWindow()
	{
		var schedule = new CarriedInventoryReportSchedule();
		schedule.Arm(0);

		schedule.Reset();

		Assert.False(schedule.IsDue(0));
		Assert.False(schedule.IsDue(600_000), "nothing is due until the next edge opens a window");
	}

	[Fact]
	public void ClockGoingBackwards_RebasesInsteadOfStalling()
	{
		var schedule = new CarriedInventoryReportSchedule();
		schedule.Arm(600_000); // armed right before Environment.TickCount would wrap
		schedule.MarkReported(600_000);

		// The reading wrapped past zero: the window must not wait 24.9 days to fire.
		Assert.True(schedule.IsDue(500), "a backwards reading re-bases the window on the new clock");

		schedule.MarkReported(500);
		Assert.False(schedule.IsDue(500 + CarriedInventoryReportSchedule.BurstIntervalMs - 1));
		Assert.True(schedule.IsDue(500 + CarriedInventoryReportSchedule.BurstIntervalMs));
	}

	[Fact]
	public void MarkReported_BeforeAnyArming_DoesNotOpenAWindow()
	{
		var schedule = new CarriedInventoryReportSchedule();

		// A stray send (a handshake-time report with nothing to register yet) spends
		// nothing: only an edge opens a window.
		schedule.MarkReported(5_000);

		Assert.False(schedule.IsDue(600_000));
	}
}
