using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.IO.Compression;
using System.Text;

namespace CasualtiesUnknownOnline.Tests.Tooling;

/// <summary>
/// The stateless harness for the compare-itemtrace.ps1 contract tests: the raw [ItemTrace] fixtures, the Windows PowerShell invocation, the gzip writer and the per-test temp directory. Every call spawns its own process and temp directory — this class holds no state.
/// </summary>
internal static class CompareItemTraceHarness
{
	internal const string SpawnPickupSimTrace = """
		[ItemTrace] op=0 begin item=42 origin=spawn event=spawn
		[ItemTrace] op=0 item=42 origin=spawn result=Committed(1) events=[spawn]
		[ItemTrace] op=1 begin item=42 origin=pickup event=pickup
		[ItemTrace] op=1 item=42 origin=pickup result=Committed(1) events=[pickup]
		""";

	internal const string WholeSessionRealLog = """
		[2026-08-16 10:00:00.000] [INF] [CasualtiesUnknownOnline.GameAdapter.Items.OperationTrace] [ItemTrace] op=7 item=0 origin=OnItemPickedUp result=Skipped events=[NoId]
		[2026-08-16 10:00:01.000] [INF] [CasualtiesUnknownOnline.GameAdapter.Items.OperationTrace] [ItemTrace] op=8 begin item=99 origin=OnItemDropped event=Drop
		[2026-08-16 10:00:01.100] [INF] [CasualtiesUnknownOnline.GameAdapter.Items.OperationTrace] [ItemTrace] op=8 item=99 origin=OnItemDropped result=Committed(1) events=[Drop]
		[2026-08-16 10:00:02.000] [INF] [CasualtiesUnknownOnline.GameAdapter.Items.OperationTrace] [ItemTrace] op=9 begin item=42 origin=spawn event=spawn
		[2026-08-16 10:00:02.100] [INF] [CasualtiesUnknownOnline.GameAdapter.Items.OperationTrace] [ItemTrace] op=9 item=42 origin=spawn result=Committed(1) events=[spawn]
		[2026-08-16 10:00:03.000] [INF] [CasualtiesUnknownOnline.GameAdapter.Items.OperationTrace] [ItemTrace] op=10 begin item=42 origin=pickup event=pickup
		[2026-08-16 10:00:03.100] [INF] [CasualtiesUnknownOnline.GameAdapter.Items.OperationTrace] [ItemTrace] op=10 item=42 origin=pickup result=Committed(1) events=[pickup]
		[2026-08-16 10:00:04.000] [INF] [CasualtiesUnknownOnline.GameAdapter.Items.OperationTrace] [ItemTrace] op=11 item=0 origin=OnItemDestroyed result=Skipped events=[NoId]
		""";

	internal static void WriteGzip(string path, string content)
	{
		var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(content);
		using var file = File.Create(path);
		using var gzip = new GZipStream(file, CompressionLevel.Fastest);
		gzip.Write(bytes, 0, bytes.Length);
	}

	internal static RunResult Run(TempFiles files, string realFileName, params string[] args)
	{
		var script = FindTool("compare-itemtrace.ps1");
		var arguments = string.Join(" ", new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", Quote(script) }
			.Concat(["-RealLog", files.Path(realFileName)])
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

	internal static string FindTool(string fileName)
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

	internal static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";

	internal sealed record RunResult(int ExitCode, string Output, string Error)
	{
		public string All => Error.Length == 0 ? Output : Output + Environment.NewLine + "[stderr] " + Error;
	}

	internal sealed class TempFiles : IDisposable
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
