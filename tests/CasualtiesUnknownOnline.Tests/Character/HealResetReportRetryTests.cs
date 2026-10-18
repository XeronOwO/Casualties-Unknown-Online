using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Character;

/// <summary>
/// The applied reset's send retry: the transport can refuse a send LOCALLY, and this event is the
/// only carrier of the three values the host owns, so a send that never happened must be re-offered
/// on the report cadence instead of being reported as sent. The budget, its named failure and the
/// drop-with-the-body/session rule are part of the contract, not implementation details.
/// </summary>
public class HealResetReportRetryTests
{
	[Fact]
	public void A_SuppressedReport_IsRetriedOnTheCadenceAndStopsWhenAccepted()
	{
		var characters = new FakeCharacterDataControl { SuppressedResetSends = 2 };
		var clock = new FakeClock();
		var log = new RecordingLogger<HealResetReportRetry>();
		var retry = new HealResetReportRetry(characters, clock, log);

		retry.Report(Applied());
		Assert.Single(characters.AppliedResetSends);
		Assert.Equal(LogLevel.Warning, log.Entries[0].Level);

		clock.Advance(HealResetReportRetry.RetryIntervalMs - 1);
		retry.Tick();
		Assert.Single(characters.AppliedResetSends);

		clock.Advance(1);
		retry.Tick();
		Assert.Equal(2, characters.AppliedResetSends.Count);

		clock.Advance(HealResetReportRetry.RetryIntervalMs);
		retry.Tick();
		Assert.Equal(3, characters.AppliedResetSends.Count);
		Assert.Contains(log.Entries, entry => entry.Level == LogLevel.Information && entry.Message.Contains("reported the applied reset"));

		clock.Advance(10_000);
		retry.Tick();
		Assert.Equal(3, characters.AppliedResetSends.Count);
	}

	[Fact]
	public void AnAlwaysSuppressedReport_GivesUpAtTheBudgetAndNamesIt()
	{
		var characters = new FakeCharacterDataControl { SuppressedResetSends = 99 };
		var clock = new FakeClock();
		var log = new RecordingLogger<HealResetReportRetry>();
		var retry = new HealResetReportRetry(characters, clock, log);

		retry.Report(Applied());
		for (var i = 0; i < HealResetReportRetry.MaxAttempts; i++)
		{
			clock.Advance(HealResetReportRetry.RetryIntervalMs);
			retry.Tick();
		}

		Assert.Equal(HealResetReportRetry.MaxAttempts, characters.AppliedResetSends.Count);
		Assert.True(log.HasError("not accepted after"));

		clock.Advance(10_000);
		retry.Tick();
		Assert.Equal(HealResetReportRetry.MaxAttempts, characters.AppliedResetSends.Count);
	}

	[Fact]
	public void ClearDropsThePendingResult_SoALaterBodyNeverReceivesIt()
	{
		var characters = new FakeCharacterDataControl { SuppressedResetSends = 99 };
		var clock = new FakeClock();
		var log = new RecordingLogger<HealResetReportRetry>();
		var retry = new HealResetReportRetry(characters, clock, log);

		retry.Report(Applied());
		retry.Clear();
		clock.Advance(10_000);
		retry.Tick();

		Assert.Single(characters.AppliedResetSends);
		Assert.True(log.Entries.Exists(entry => entry.Message.Contains("dropped the pending applied-reset report")));
	}

	[Fact]
	public void A_BackwardsClock_DoesNotStrandThePendingReport()
	{
		var characters = new FakeCharacterDataControl { SuppressedResetSends = 1 };
		var clock = new FakeClock();
		var log = new RecordingLogger<HealResetReportRetry>();
		var retry = new HealResetReportRetry(characters, clock, log);

		retry.Report(Applied());
		clock.Advance(-5_000);
		retry.Tick();

		Assert.Equal(2, characters.AppliedResetSends.Count);
	}

	private static HealResetAppliedMsg Applied() => new()
	{
		OwnerSteamId = 7,
		InstructionId = 3,
		Health = new CharacterHealthMsg { BloodVolume = 100f },
		Limbs = [new CharacterLimbMsg { Index = 0 }],
	};
}
