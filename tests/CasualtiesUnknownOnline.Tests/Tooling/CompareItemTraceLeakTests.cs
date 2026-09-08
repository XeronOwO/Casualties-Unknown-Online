using Xunit;
using static CasualtiesUnknownOnline.Tests.Tooling.CompareItemTraceHarness;

namespace CasualtiesUnknownOnline.Tests.Tooling;

/// <summary>
/// compare-itemtrace.ps1 family: the begin-without-end leak handling (expected unresolved begin fails; real-log leak warns by default and fails with -FailOnLeak).
/// Split from the former single class so xUnit v2 (serial inside a class)
/// does not serialize this whole surface in one collection.
/// </summary>
[Trait("Category", "Integration")]
public class CompareItemTraceLeakTests
{
	[Fact]
	public void ExpectedUnresolvedBegin_Fails()
	{
		const string leakedSimTrace = """
			[ItemTrace] op=0 begin item=42 origin=spawn event=spawn
			[ItemTrace] op=1 begin item=43 origin=pickup event=pickup
			""";
		using var files = TempFiles.Create(("real.log", WholeSessionRealLog), ("sim.trace", leakedSimTrace));
		var result = Run(files, "real.log", "-Replay", "leak", "-SimTrace", files.Path("sim.trace"));

		Assert.True(result.ExitCode == 1, result.All);
		Assert.Contains("begin-without-end leak", result.Output);
		Assert.Contains("op=0 (spawn)", result.Output);
		Assert.Contains("op=1 (pickup)", result.Output);
	}

	[Fact]
	public void RealUnresolvedBegin_WarnsByDefault_AndFailsWithFailOnLeak()
	{
		const string realWithLeak = """
			[ItemTrace] op=0 begin item=42 origin=spawn event=spawn
			[ItemTrace] op=0 item=42 origin=spawn result=Committed(1) events=[spawn]
			[ItemTrace] op=1 begin item=43 origin=pickup event=pickup
			""";
		const string spawnSimTrace = """
			[ItemTrace] op=0 begin item=42 origin=spawn event=spawn
			[ItemTrace] op=0 item=42 origin=spawn result=Committed(1) events=[spawn]
			""";
		using var files = TempFiles.Create(("real.log", realWithLeak), ("sim.trace", spawnSimTrace));

		var warning = Run(files, "real.log", "-Replay", "spawn", "-SimTrace", files.Path("sim.trace"));
		Assert.True(warning.ExitCode == 0, warning.All);
		Assert.Contains("WARNING: real log has 1 begin-without-end leak", warning.Output);

		var fail = Run(files, "real.log", "-Replay", "spawn", "-SimTrace", files.Path("sim.trace"), "-FailOnLeak");
		Assert.True(fail.ExitCode == 1, fail.All);
		Assert.Contains("SIMTRACE DIFF FAILED", fail.Output);
		Assert.Contains("begin-without-end leak", fail.Output);
	}
}
