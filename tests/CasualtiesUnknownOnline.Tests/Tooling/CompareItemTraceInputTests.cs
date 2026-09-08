using Xunit;
using static CasualtiesUnknownOnline.Tests.Tooling.CompareItemTraceHarness;

namespace CasualtiesUnknownOnline.Tests.Tooling;

/// <summary>
/// compare-itemtrace.ps1 family: the input surfaces (gzip real log, -NoBegins result-only compare, missing files/trace lines).
/// Split from the former single class so xUnit v2 (serial inside a class)
/// does not serialize this whole surface in one collection.
/// </summary>
[Trait("Category", "Integration")]
public class CompareItemTraceInputTests
{
	[Fact]
	public void GzipRealLog_IsReadDirectly()
	{
		using var files = TempFiles.Create(("sim.trace", SpawnPickupSimTrace));
		WriteGzip(files.Path("real.log.gz"), WholeSessionRealLog);

		var result = Run(files, "real.log.gz", "-Replay", "spawn-pickup", "-SimTrace", files.Path("sim.trace"));

		Assert.True(result.ExitCode == 0, result.All);
		Assert.Contains("SIMTRACE DIFF PASSED", result.Output);
		Assert.Contains("original log lines 4-7", result.Output);
	}

	[Fact]
	public void NoBegins_ComparesResultSequenceOnly()
	{
		const string differentBeginRealLog = """
			[ItemTrace] op=0 begin item=42 origin=other event=other
			[ItemTrace] op=0 item=42 origin=spawn result=Committed(1) events=[spawn]
			""";
		const string spawnSimTrace = """
			[ItemTrace] op=0 begin item=42 origin=spawn event=spawn
			[ItemTrace] op=0 item=42 origin=spawn result=Committed(1) events=[spawn]
			""";
		using var files = TempFiles.Create(("real.log", differentBeginRealLog), ("sim.trace", spawnSimTrace));

		var defaultResult = Run(files, "real.log", "-Replay", "spawn", "-SimTrace", files.Path("sim.trace"));
		Assert.True(defaultResult.ExitCode == 1, defaultResult.All);

		var noBegins = Run(files, "real.log", "-Replay", "spawn", "-SimTrace", files.Path("sim.trace"), "-NoBegins");
		Assert.True(noBegins.ExitCode == 0, noBegins.All);
		Assert.Contains("begins ignored", noBegins.Output);
	}

	[Fact]
	public void MissingTraceLinesOrFiles_FailLoudly()
	{
		const string noTraces = "[2026-08-16 10:00:00.000] [INF] [Category] an unrelated line";
		const string spawnSimTrace = """
			[ItemTrace] op=0 begin item=42 origin=spawn event=spawn
			[ItemTrace] op=0 item=42 origin=spawn result=Committed(1) events=[spawn]
			""";
		using var files = TempFiles.Create(("real.log", noTraces), ("sim.trace", spawnSimTrace));
		var emptyReal = Run(files, "real.log", "-Replay", "spawn", "-SimTrace", files.Path("sim.trace"));
		Assert.True(emptyReal.ExitCode == 1, emptyReal.All);
		Assert.Contains("no [ItemTrace] lines", emptyReal.Output);

		var missingSim = Run(files, "real.log", "-Replay", "spawn", "-SimTrace", files.Path("missing.trace"));
		Assert.True(missingSim.ExitCode == 1, missingSim.All);
		Assert.Contains("not found", missingSim.Output);
	}
}
