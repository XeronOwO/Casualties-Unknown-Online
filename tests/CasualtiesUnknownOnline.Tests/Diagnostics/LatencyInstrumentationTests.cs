using System.Linq;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Diagnostics;

public sealed class LatencyInstrumentationTests
{
	private static LatencyInstrumentation Create(MutableOptionsMonitor<LatencyOptions> monitor) =>
		new(monitor, NullLogger<LatencyInstrumentation>.Instance);

	[Fact]
	public void Disabled_MeasureInvokesActionAndDoesNotCollect()
	{
		var monitor = new MutableOptionsMonitor<LatencyOptions>(new LatencyOptions { Enabled = false });
		var instrumentation = Create(monitor);
		var called = false;

		instrumentation.Measure("Run", () => called = true);

		Assert.True(called);
		Assert.Empty(instrumentation.CurrentSamples);
	}

	[Fact]
	public void Enabled_MeasureAggregatesCallsAndFlushClears()
	{
		var monitor = new MutableOptionsMonitor<LatencyOptions>(new LatencyOptions
		{
			Enabled = true,
			LogIntervalSeconds = 0,
		});
		var instrumentation = Create(monitor);

		instrumentation.Measure("Run", () => { });
		instrumentation.Measure("Run", () => { });
		instrumentation.Measure("Fluid", () => { });

		var run = instrumentation.CurrentSamples["Run"];
		var fluid = instrumentation.CurrentSamples["Fluid"];
		Assert.Equal(2, run.Calls);
		Assert.True(run.TotalMs >= 0);
		Assert.True(run.MaxMs >= run.AverageMs);
		Assert.Equal(1, fluid.Calls);
		Assert.True(fluid.AverageMs >= 0);

		instrumentation.Flush();

		Assert.Empty(instrumentation.CurrentSamples);
	}

	[Fact]
	public void Enabled_MeasureScopeRecordsOnDispose()
	{
		var monitor = new MutableOptionsMonitor<LatencyOptions>(new LatencyOptions
		{
			Enabled = true,
			LogIntervalSeconds = 0,
		});
		var instrumentation = Create(monitor);

		using (instrumentation.Measure("Fluid"))
		{
		}

		Assert.Single(instrumentation.CurrentSamples);
		Assert.Equal(1, instrumentation.CurrentSamples["Fluid"].Calls);
	}

	[Fact]
	public void ToggleOff_StopsCollectingWithoutLosingExistingSamplesUntilFlush()
	{
		var monitor = new MutableOptionsMonitor<LatencyOptions>(new LatencyOptions
		{
			Enabled = true,
			LogIntervalSeconds = 0,
		});
		var instrumentation = Create(monitor);

		instrumentation.Measure("Run", () => { });

		monitor.Set(new LatencyOptions { Enabled = false });
		instrumentation.Measure("Fluid", () => { });

		// Disabling stops new collection; the already-aggregated Run sample stays
		// until the next enabled flush.
		Assert.Single(instrumentation.CurrentSamples);
		Assert.Equal("Run", instrumentation.CurrentSamples.Keys.Single());
	}

	[Fact]
	public void Disabled_RecordFrameDoesNotCollect()
	{
		var monitor = new MutableOptionsMonitor<LatencyOptions>(new LatencyOptions { Enabled = false });
		var instrumentation = Create(monitor);

		instrumentation.RecordFrame(42.0);

		Assert.Equal(0, instrumentation.CurrentFrame.Calls);
	}

	[Fact]
	public void Enabled_RecordFrameAggregatesCallsAndSlowCount()
	{
		var monitor = new MutableOptionsMonitor<LatencyOptions>(new LatencyOptions
		{
			Enabled = true,
			LogIntervalSeconds = 0,
			SlowFrameThresholdMs = 25.0,
		});
		var instrumentation = Create(monitor);

		instrumentation.RecordFrame(10.0);
		instrumentation.RecordFrame(30.0);

		var frame = instrumentation.CurrentFrame;
		Assert.Equal(2, frame.Calls);
		Assert.Equal(40.0, frame.TotalMs, 3);
		Assert.Equal(20.0, frame.AverageMs, 3);
		Assert.Equal(30.0, frame.MaxMs, 3);
		Assert.Equal(1, frame.SlowCalls);
	}

	[Fact]
	public void Flush_ClearsFrameSummary()
	{
		var monitor = new MutableOptionsMonitor<LatencyOptions>(new LatencyOptions
		{
			Enabled = true,
			LogIntervalSeconds = 0,
			SlowFrameThresholdMs = 25.0,
		});
		var instrumentation = Create(monitor);

		instrumentation.RecordFrame(10.0);
		instrumentation.Flush();

		Assert.Equal(0, instrumentation.CurrentFrame.Calls);
	}

}
