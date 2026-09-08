using Xunit;
using static CasualtiesUnknownOnline.Tests.Tooling.CompareItemTraceHarness;

namespace CasualtiesUnknownOnline.Tests.Tooling;

/// <summary>
/// compare-itemtrace.ps1 family: the matching modes (subsequence pass/mismatch, contiguous rejection, strict window).
/// Split from the former single class so xUnit v2 (serial inside a class)
/// does not serialize this whole surface in one collection.
/// </summary>
public class CompareItemTraceMatchModeTests
{
	[Fact]
	public void SubsequenceMatch_WholeSessionLog_PassesAndLocatesOriginalLines()
	{
		using var files = TempFiles.Create(("real.log", WholeSessionRealLog), ("sim.trace", SpawnPickupSimTrace));
		var result = Run(files, "real.log", "-Replay", "spawn-pickup", "-SimTrace", files.Path("sim.trace"));

		Assert.True(result.ExitCode == 0, result.All);
		Assert.Contains("SIMTRACE DIFF PASSED", result.Output);
		Assert.Contains("subsequence mode", result.Output);
		Assert.Contains("original log lines 4-7", result.Output);
	}

	[Fact]
	public void SubsequenceMismatch_FailsAndPrintsBothTokenSequences()
	{
		const string badSimTrace = """
			[ItemTrace] op=0 begin item=42 origin=spawn event=spawn
			[ItemTrace] op=0 item=42 origin=spawn result=Skipped events=[spawn]
			""";
		using var files = TempFiles.Create(("real.log", WholeSessionRealLog), ("sim.trace", badSimTrace));
		var result = Run(files, "real.log", "-Replay", "bad", "-SimTrace", files.Path("sim.trace"));

		Assert.True(result.ExitCode == 1, result.All);
		Assert.Contains("SIMTRACE DIFF FAILED", result.Output);
		Assert.Contains("end:Committed(1):spawn", result.Output);
		Assert.Contains("end:Skipped:spawn", result.Output);
	}

	[Fact]
	public void Contiguous_RejectsInterleavedNoise_WhileSubsequenceAcceptsIt()
	{
		const string interleavedRealLog = """
			[ItemTrace] op=0 begin item=42 origin=spawn event=spawn
			[ItemTrace] op=1 item=0 origin=OnItemDestroyed result=Skipped events=[NoId]
			[ItemTrace] op=0 item=42 origin=spawn result=Committed(1) events=[spawn]
			""";
		const string spawnSimTrace = """
			[ItemTrace] op=0 begin item=42 origin=spawn event=spawn
			[ItemTrace] op=0 item=42 origin=spawn result=Committed(1) events=[spawn]
			""";
		using var files = TempFiles.Create(("real.log", interleavedRealLog), ("sim.trace", spawnSimTrace));

		var subsequence = Run(files, "real.log", "-Replay", "spawn", "-SimTrace", files.Path("sim.trace"));
		Assert.True(subsequence.ExitCode == 0, subsequence.All);

		var contiguous = Run(files, "real.log", "-Replay", "spawn", "-SimTrace", files.Path("sim.trace"), "-Contiguous");
		Assert.True(contiguous.ExitCode == 1, contiguous.All);
		Assert.Contains("SIMTRACE DIFF FAILED", contiguous.Output);
	}

	[Fact]
	public void Strict_RequiresExactWindow()
	{
		const string exactRealLog = """
			[ItemTrace] op=0 begin item=42 origin=spawn event=spawn
			[ItemTrace] op=0 item=42 origin=spawn result=Committed(1) events=[spawn]
			[ItemTrace] op=1 begin item=42 origin=pickup event=pickup
			[ItemTrace] op=1 item=42 origin=pickup result=Committed(1) events=[pickup]
			""";
		using var exact = TempFiles.Create(("real.log", exactRealLog), ("sim.trace", SpawnPickupSimTrace));
		var pass = Run(exact, "real.log", "-Replay", "spawn-pickup", "-SimTrace", exact.Path("sim.trace"), "-Strict");
		Assert.True(pass.ExitCode == 0, pass.All);

		using var noisy = TempFiles.Create(("real.log", WholeSessionRealLog), ("sim.trace", SpawnPickupSimTrace));
		var fail = Run(noisy, "real.log", "-Replay", "spawn-pickup", "-SimTrace", noisy.Path("sim.trace"), "-Strict");
		Assert.True(fail.ExitCode == 1, fail.All);
	}
}
