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
}
