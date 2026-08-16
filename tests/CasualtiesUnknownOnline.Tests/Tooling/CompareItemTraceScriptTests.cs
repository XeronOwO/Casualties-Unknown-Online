using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.IO.Compression;
using System.Text;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling;

/// <summary>
/// L0 contract for the real-log vs replay SimTrace diff tool
/// (tools/compare-itemtrace.ps1). The script is exercised end-to-end through
/// Windows PowerShell against raw [ItemTrace] fixtures: the normalization
/// surface (origin/item/op ids are dropped; begin-event, result and the event
/// chain are compared), the three matching modes (subsequence / contiguous /
/// strict), the -NoBegins result-only surface and the begin-without-end leak
/// handling. These tests are the "assertion-effectiveness proof" for the tool:
/// a broken matcher, a too-lenient normalizer or a swallowed leak must turn a
/// case red.
/// </summary>
public class CompareItemTraceScriptTests
{
	private const string SpawnPickupSimTrace = """
		[ItemTrace] op=0 begin item=42 origin=spawn event=spawn
		[ItemTrace] op=0 item=42 origin=spawn result=Committed(1) events=[spawn]
		[ItemTrace] op=1 begin item=42 origin=pickup event=pickup
		[ItemTrace] op=1 item=42 origin=pickup result=Committed(1) events=[pickup]
		""";

	private const string WholeSessionRealLog = """
		[2026-08-16 10:00:00.000] [INF] [CasualtiesUnknownOnline.GameAdapter.Items.OperationTrace] [ItemTrace] op=7 item=0 origin=OnItemPickedUp result=Skipped events=[NoId]
		[2026-08-16 10:00:01.000] [INF] [CasualtiesUnknownOnline.GameAdapter.Items.OperationTrace] [ItemTrace] op=8 begin item=99 origin=OnItemDropped event=Drop
		[2026-08-16 10:00:01.100] [INF] [CasualtiesUnknownOnline.GameAdapter.Items.OperationTrace] [ItemTrace] op=8 item=99 origin=OnItemDropped result=Committed(1) events=[Drop]
		[2026-08-16 10:00:02.000] [INF] [CasualtiesUnknownOnline.GameAdapter.Items.OperationTrace] [ItemTrace] op=9 begin item=42 origin=spawn event=spawn
		[2026-08-16 10:00:02.100] [INF] [CasualtiesUnknownOnline.GameAdapter.Items.OperationTrace] [ItemTrace] op=9 item=42 origin=spawn result=Committed(1) events=[spawn]
		[2026-08-16 10:00:03.000] [INF] [CasualtiesUnknownOnline.GameAdapter.Items.OperationTrace] [ItemTrace] op=10 begin item=42 origin=pickup event=pickup
		[2026-08-16 10:00:03.100] [INF] [CasualtiesUnknownOnline.GameAdapter.Items.OperationTrace] [ItemTrace] op=10 item=42 origin=pickup result=Committed(1) events=[pickup]
		[2026-08-16 10:00:04.000] [INF] [CasualtiesUnknownOnline.GameAdapter.Items.OperationTrace] [ItemTrace] op=11 item=0 origin=OnItemDestroyed result=Skipped events=[NoId]
		""";

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

	private static void WriteGzip(string path, string content)
	{
		var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(content);
		using var file = File.Create(path);
		using var gzip = new GZipStream(file, CompressionLevel.Fastest);
		gzip.Write(bytes, 0, bytes.Length);
	}

	private static RunResult Run(TempFiles files, string realFileName, params string[] args)
	{
		var script = FindTool("compare-itemtrace.ps1");
		var arguments = string.Join(" ", new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", Quote(script) }
			.Concat(new[] { "-RealLog", files.Path(realFileName) })
			.Concat(args.Select(Quote)));

		var startInfo = new ProcessStartInfo("powershell.exe", arguments)
		{
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			StandardOutputEncoding = Encoding.UTF8,
			StandardErrorEncoding = Encoding.UTF8,
			CreateNoWindow = true,
		};

		using var process = Process.Start(startInfo)
			?? throw new InvalidOperationException("powershell.exe did not start");
		var stdout = process.StandardOutput.ReadToEnd();
		var stderr = process.StandardError.ReadToEnd();
		process.WaitForExit();
		return new RunResult(process.ExitCode, stdout, stderr);
	}

	private static string FindTool(string fileName)
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory != null)
		{
			var candidate = Path.Combine(directory.FullName, "tools", fileName);
			if (File.Exists(candidate))
			{
				return candidate;
			}

			directory = directory.Parent;
		}

		throw new InvalidOperationException($"tools/{fileName} not found above {AppContext.BaseDirectory}");
	}

	private static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";

	private sealed record RunResult(int ExitCode, string Output, string Error)
	{
		public string All => Error.Length == 0 ? Output : Output + Environment.NewLine + "[stderr] " + Error;
	}

	private sealed class TempFiles : IDisposable
	{
		private readonly string _directory;

		private TempFiles(string directory)
		{
			_directory = directory;
		}

		public static TempFiles Create(params (string Name, string Content)[] files)
		{
			var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cuo-compare-itemtrace-tests", Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(directory);
			foreach (var (name, content) in files)
			{
				File.WriteAllText(System.IO.Path.Combine(directory, name), content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			}

			return new TempFiles(directory);
		}

		public string Path(string name) => System.IO.Path.Combine(_directory, name);

		public void Dispose()
		{
			foreach (var file in Directory.EnumerateFiles(_directory))
			{
				File.Delete(file);
			}

			Directory.Delete(_directory);
		}
	}
}
