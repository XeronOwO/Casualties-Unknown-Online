using System;
using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Character;

/// <summary>
/// The host vitals pass's frame-cost readout: one Debug line per ten-second window, carrying the
/// per-clone step and native-pass cost, the cost of one fixed-step invocation across every
/// simulated clone, its share of wall time, and the fixed step/time scale the window ran on. The
/// line is the ticket's affordability evidence, so the window boundary, every field's arithmetic,
/// the basis and quiet-gap resets, the Debug gate and the slowest-step maxima are pinned here; the
/// call site that feeds it is pinned by source in <c>HostVitalsSuppressionPinTests</c>.
/// </summary>
public class HostVitalsCostProbeTests
{
	private const long TicksPerSecond = 1_000_000;

	private const long FrameTicks = 20_000; // the 50 Hz fixed step

	private const long StepTicks = 100; // 100 us whole step

	private const long PassTicks = 80; // 80 us native pass inside it

	private const float FixedStepSeconds = 0.02f;

	[Fact]
	public void AWindow_EmitsOneDebugLineOnlyAfterTheInterval()
	{
		var log = new RecordingLogger<HostVitalsCostProbe>();
		var probe = new HostVitalsCostProbe(log, TicksPerSecond);
		var start = 5_000_000L;

		RecordFrames(probe, start, frames: 500, steamId: 1);

		Assert.Empty(log.Entries); // 9.98 s: one frame short of the window

		RecordFrame(probe, start + (500 * FrameTicks), steamId: 1);

		var entry = Assert.Single(log.Entries);
		Assert.Equal(LogLevel.Debug, entry.Level);
		Assert.Contains("cost 10.0s", entry.Message, StringComparison.Ordinal);
		Assert.Contains("bodies=1", entry.Message, StringComparison.Ordinal);
		Assert.Contains("steps=501", entry.Message, StringComparison.Ordinal);
		Assert.Contains("passes=501", entry.Message, StringComparison.Ordinal);
		Assert.Contains("timeScale=1", entry.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void TheSummary_NamesThePerCloneStepCostAndThePerFixedFrameTotal()
	{
		var log = new RecordingLogger<HostVitalsCostProbe>();
		var probe = new HostVitalsCostProbe(log, TicksPerSecond);
		RecordFrames(probe, 5_000_000L, frames: 501, steamId: 1);

		var message = Assert.Single(log.Entries).Message;

		Assert.Contains("stepAvg=100.0us", message, StringComparison.Ordinal);
		Assert.Contains("stepMax=100.0us", message, StringComparison.Ordinal);
		Assert.Contains("passAvg=80.0us", message, StringComparison.Ordinal);
		Assert.Contains("passMax=80.0us", message, StringComparison.Ordinal);
		// 501 steps x 100 us over 10 s = 0.501% of wall time; at a 20 ms fixed step and timeScale 1
		// that is 0.1002 ms per fixed-step invocation -> 0.100 ms, and 0.50% of the wall budget.
		Assert.Contains("frameCost=0.100ms", message, StringComparison.Ordinal);
		Assert.Contains("frameBudget=0.50%", message, StringComparison.Ordinal);
		Assert.Contains("fixedStep=20.00ms", message, StringComparison.Ordinal);
	}

	[Fact]
	public void ATenTimesScale_KeepsThePerInvocationCost_AndRaisesOnlyTheWallShare()
	{
		var log = new RecordingLogger<HostVitalsCostProbe>();
		var probe = new HostVitalsCostProbe(log, TicksPerSecond);
		// At 5x the fixed step is invoked every 4 ms of wall time: 2501 invocations in the window.
		RecordFrames(probe, 5_000_000L, frames: 2501, steamId: 1, frameTicks: FrameTicks / 5, timeScale: 5f);

		var message = Assert.Single(log.Entries).Message;

		// The same per-invocation cost as the 1x window, over five times as many invocations per
		// wall second: the wall share rises 5x, the per-invocation number does not.
		Assert.Contains("stepAvg=100.0us", message, StringComparison.Ordinal);
		Assert.Contains("frameCost=0.100ms", message, StringComparison.Ordinal);
		Assert.Contains("frameBudget=2.50%", message, StringComparison.Ordinal);
		Assert.Contains("timeScale=5", message, StringComparison.Ordinal);
	}

	[Fact]
	public void ASpeedChange_ReportsTheOldWindowOnItsOwnBasis_AndStartsANewOne()
	{
		var log = new RecordingLogger<HostVitalsCostProbe>();
		var probe = new HostVitalsCostProbe(log, TicksPerSecond);
		var start = 5_000_000L;

		RecordFrames(probe, start, frames: 250, steamId: 1); // 5 s at timeScale 1
		var changeAt = start + (250 * FrameTicks);
		RecordFrame(probe, changeAt, steamId: 1, timeScale: 5f);
		RecordFrames(probe, changeAt + FrameTicks, frames: 500, steamId: 1, timeScale: 5f);

		Assert.Equal(2, log.Entries.Count);
		Assert.Contains("cost 5.0s", log.Entries[0].Message, StringComparison.Ordinal);
		Assert.Contains("steps=250", log.Entries[0].Message, StringComparison.Ordinal);
		Assert.Contains("timeScale=1", log.Entries[0].Message, StringComparison.Ordinal);
		Assert.Contains("cost 10.0s", log.Entries[1].Message, StringComparison.Ordinal);
		Assert.Contains("steps=501", log.Entries[1].Message, StringComparison.Ordinal);
		Assert.Contains("timeScale=5", log.Entries[1].Message, StringComparison.Ordinal);
	}

	[Fact]
	public void APausedGap_ReportsThePartialWindow_ThenRestarts()
	{
		var log = new RecordingLogger<HostVitalsCostProbe>();
		var probe = new HostVitalsCostProbe(log, TicksPerSecond);
		var start = 5_000_000L;

		RecordFrames(probe, start, frames: 100, steamId: 1); // 2 s of cost
		Assert.Empty(log.Entries);
		var gapStart = start + (250 * FrameTicks); // a 3 s quiet host
		RecordFrame(probe, gapStart, steamId: 1); // closes the 2 s window
		RecordFrames(probe, gapStart + FrameTicks, frames: 500, steamId: 1);

		Assert.Equal(2, log.Entries.Count);
		Assert.Contains("cost 2.0s", log.Entries[0].Message, StringComparison.Ordinal);
		Assert.Contains("steps=100", log.Entries[0].Message, StringComparison.Ordinal);
		Assert.Contains("cost 10.0s", log.Entries[1].Message, StringComparison.Ordinal);
		Assert.Contains("steps=501", log.Entries[1].Message, StringComparison.Ordinal);
	}

	[Fact]
	public void TheWindow_ReportsTheSlowestStepAndPass_NotTheAverage()
	{
		var log = new RecordingLogger<HostVitalsCostProbe>();
		var probe = new HostVitalsCostProbe(log, TicksPerSecond);
		var start = 5_000_000L;

		RecordFrames(probe, start, frames: 500, steamId: 1);
		RecordFrame(probe, start + (500 * FrameTicks), steamId: 1, stepTicks: 500, passTicks: 400);

		var message = Assert.Single(log.Entries).Message;
		Assert.Contains("stepAvg=100.8us", message, StringComparison.Ordinal);
		Assert.Contains("stepMax=500.0us", message, StringComparison.Ordinal);
		Assert.Contains("passAvg=80.6us", message, StringComparison.Ordinal);
		Assert.Contains("passMax=400.0us", message, StringComparison.Ordinal);
	}

	[Fact]
	public void WhileDebugIsFiltered_NothingIsAccumulated_AndEnablingStartsAFreshWindow()
	{
		var log = new FilteredLogger(LogLevel.Debug);
		var probe = new HostVitalsCostProbe(log, TicksPerSecond);
		var start = 5_000_000L;

		RecordFrames(probe, start, frames: 100, steamId: 1, stepTicks: 200, passTicks: 160);
		log.MinimumLevel = LogLevel.Information;
		for (var frame = 0; frame < 20; frame++)
		{
			probe.Suspend(); // the caller's filtered path: no record, no window state
		}

		Assert.Empty(log.Entries);

		log.MinimumLevel = LogLevel.Debug;
		var resume = start + (120 * FrameTicks);
		RecordFrames(probe, resume, frames: 501, steamId: 1);

		var message = Assert.Single(log.Entries).Message;
		Assert.Contains("steps=501", message, StringComparison.Ordinal); // not 621
		Assert.Contains("stepAvg=100.0us", message, StringComparison.Ordinal); // not the mixed 200 us phase
		Assert.Contains("passAvg=80.0us", message, StringComparison.Ordinal);
	}

	[Fact]
	public void TheWindow_CountsDistinctBodies_AndOnlyStepsThatRanThePass()
	{
		var log = new RecordingLogger<HostVitalsCostProbe>();
		var probe = new HostVitalsCostProbe(log, TicksPerSecond);
		var start = 5_000_000L;

		for (var frame = 0; frame <= 500; frame++)
		{
			var now = start + (frame * FrameTicks);
			probe.Record(now, now - StepTicks, PassTicks, steamId: 1, FixedStepSeconds, timeScale: 1f);
			// Body 2 is in world but has no snapshot yet: its step returns before the pass.
			probe.Record(now, now - (StepTicks / 2), passTicks: 0, steamId: 2, FixedStepSeconds, timeScale: 1f);
		}

		var message = Assert.Single(log.Entries).Message;
		Assert.Contains("bodies=2", message, StringComparison.Ordinal);
		Assert.Contains("steps=1001", message, StringComparison.Ordinal);
		Assert.Contains("passes=501", message, StringComparison.Ordinal);
		Assert.Contains("passAvg=80.0us", message, StringComparison.Ordinal);
		// 501 x 100 us + 500 x 50 us over 10 s = 0.1502 ms per fixed-step invocation at timeScale 1.
		Assert.Contains("frameCost=0.150ms", message, StringComparison.Ordinal);
		Assert.Contains("frameBudget=0.75%", message, StringComparison.Ordinal);
	}

	private static void RecordFrames(HostVitalsCostProbe probe, long start, int frames, ulong steamId,
		long stepTicks = StepTicks, long passTicks = PassTicks, float timeScale = 1f, long frameTicks = FrameTicks)
	{
		for (var frame = 0; frame < frames; frame++)
		{
			RecordFrame(probe, start + (frame * frameTicks), steamId, stepTicks, passTicks, timeScale);
		}
	}

	private static void RecordFrame(HostVitalsCostProbe probe, long now, ulong steamId,
		long stepTicks = StepTicks, long passTicks = PassTicks, float timeScale = 1f) =>
		probe.Record(now, now - stepTicks, passTicks, steamId, FixedStepSeconds, timeScale);

	/// <summary>A logger whose Debug gate can be opened mid-test; entries below the minimum are dropped like a real provider.</summary>
	private sealed class FilteredLogger(LogLevel minimumLevel) : ILogger
	{
		internal List<(LogLevel Level, string Message)> Entries { get; } = [];

		internal LogLevel MinimumLevel { get; set; } = minimumLevel;

		public IDisposable? BeginScope<TState>(TState state)
			where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => logLevel >= MinimumLevel;

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
		{
			if (IsEnabled(logLevel))
			{
				Entries.Add((logLevel, formatter(state, exception)));
			}
		}
	}
}
