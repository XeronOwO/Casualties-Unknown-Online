using System.Diagnostics;

namespace CasualtiesUnknownOnline.AcceptanceHost;

/// <summary>
/// The batch command the run script carried: build the suite before any client starts, launch the pair,
/// then start the prebuilt suite and print the three timings separately, so a regression cannot hide
/// behind one number. A build must not leave MSBuild nodes behind — the suite runs repeatedly.
/// </summary>
internal static class RunCommand
{
	private const string Usage =
		"run --game-dir <install> [--out <dir>] [--suite <name>] [--fresh] [--parallel-setup] [--no-launch] [--reuse] " +
		"[--delay-between-cases <s>] [--all] [--pids <file>] " +
		"[--steam-exe <exe>] [--sandboxie <start.exe>] [--guest-box <box>] [--guest-root <install>] " +
		"[--alt-box <box>] [--alt-root <install>] [--host-url <ws>] [--guest-url <ws>] [--alt-url <ws>] " +
		"[--frames <dir>] [--frames-interval-ms <n>]";

	public static int Run(string[] args)
	{
		var arguments = new Arguments(args, Usage);
		var repoRoot = RepoPaths.RepositoryRoot();
		var project = Path.Combine(repoRoot, "tests", "CasualtiesUnknownOnline.AcceptanceSuite");
		var outDir = arguments.Optional("out") ?? Path.Combine(repoRoot, ".acceptance", "suite");
		var reportDir = Path.Combine(outDir, "report");
		var saveRoot = Path.Combine(outDir, "saves");
		var pidsPath = arguments.Optional("pids") ?? Path.Combine(outDir, "pids.json");
		var gameDir = arguments.Required("game-dir");
		Directory.CreateDirectory(outDir);

		Environment.SetEnvironmentVariable("MSBUILDDISABLENODEREUSE", "1");
		Environment.SetEnvironmentVariable("DOTNET_CLI_USE_MSBUILD_SERVER", "0");

		var buildWatch = Stopwatch.StartNew();
		if (arguments.Optional("no-build") is null)
		{
			var buildExit = RunProcess("dotnet", ["build", project, "--nologo", "-nodeReuse:false", "-p:UseSharedCompilation=false"], repoRoot);
			if (buildExit != 0)
			{
				Console.Error.WriteLine("the acceptance suite did not build");
				return buildExit;
			}
		}

		buildWatch.Stop();

		var launchWatch = Stopwatch.StartNew();
		if (arguments.Optional("no-launch") is null)
		{
			var launchArgs = new List<string>
			{
				"launch",
				"--game-dir", gameDir,
				"--out-dir", outDir,
				"--save-root", saveRoot,
				"--pids", pidsPath,
			};
			// The third client is a row's own requirement, not an extra: `two-member-continue` reads both members'
			// views, and a slice that never started the alt endpoint failed the row as "the driver at
			// ws://127.0.0.1:18592 did not answer: Unable to connect to the remote server" (measured 2026-11-27 00:28).
			var suiteNames = arguments.Optional("suite") ?? "all";
			if (arguments.Optional("all") is not null
				|| string.Equals(suiteNames, "all", StringComparison.Ordinal)
				|| suiteNames.Contains("two-member", StringComparison.Ordinal))
			{
				launchArgs.Add("--all");
			}

			// `facts` and `host-box` belong here too: a group started with its own facts file launched the *default*
			// group's clients, and its `--restart` then stopped them, which read as the suite dying with -532462766
			// (measured 2026-11-26 23:05).
			foreach (var name in new[] { "facts", "host-box", "steam-exe", "sandboxie", "guest-box", "guest-root", "alt-box", "alt-root", "host-url", "guest-url", "alt-url" })
			{
				var value = arguments.Optional(name);
				if (value is not null)
				{
					launchArgs.Add("--" + name);
					launchArgs.Add(value);
				}
			}

			if (arguments.Optional("reuse") is not null)
			{
				launchArgs.Add("--reuse");
			}
			else
			{
				launchArgs.Add("--restart");
			}

			var launchExit = LaunchCommand.Run([.. launchArgs]);
			if (launchExit != 0)
			{
				Console.Error.WriteLine("the acceptance clients did not start");
				return launchExit;
			}
		}

		launchWatch.Stop();

		var suiteDll = Path.Combine(project, "bin", "Debug", "net8.0", "CasualtiesUnknownOnline.AcceptanceSuite.dll");
		if (!File.Exists(suiteDll))
		{
			Console.Error.WriteLine($"the suite was built but {suiteDll} is missing");
			return 1;
		}

		var suiteArgs = new List<string>
		{
			"--suite", arguments.Optional("suite") ?? "all",
			"--out", reportDir,
			"--game-dir", gameDir,
			"--save-root", saveRoot,
			"--pids", pidsPath,
		};
		if (arguments.Optional("fresh") is not null)
		{
			suiteArgs.Add("--fresh");
		}

		if (arguments.Optional("parallel-setup") is not null)
		{
			suiteArgs.Add("--parallel-setup");
		}

		var delay = arguments.Optional("delay-between-cases");
		if (delay is not null)
		{
			suiteArgs.Add("--delay-between-cases");
			suiteArgs.Add(delay);
		}

		var framesDir = arguments.Optional("frames");
		var framesInterval = arguments.OptionalNumber("frames-interval-ms", 700);
		using var frames = framesDir is null ? null : new FrameRecorder(Path.Combine(outDir, framesDir), pidsPath, framesInterval);

		var suiteWatch = Stopwatch.StartNew();
		var suiteExit = RunProcess("dotnet", [suiteDll, .. suiteArgs], repoRoot);
		suiteWatch.Stop();
		if (frames is not null)
		{
			Console.WriteLine($"frames: {frames.Captured} frame(s) under {Path.Combine(outDir, framesDir!)}");
		}
		Console.WriteLine(
			$"suite build: {buildWatch.Elapsed.TotalSeconds:F1}s; launch to endpoints: {launchWatch.Elapsed.TotalSeconds:F1}s; " +
			$"suite wall: {suiteWatch.Elapsed.TotalSeconds:F1}s; exit: {suiteExit}");
		return suiteExit;
	}

	/// <summary>Runs a child with inherited console, so the suite's own output stays live and colourable.</summary>
	internal static int RunProcess(string fileName, string[] arguments, string workingDirectory)
	{
		var startInfo = new ProcessStartInfo(fileName)
		{
			WorkingDirectory = workingDirectory,
			UseShellExecute = false,
		};
		foreach (var argument in arguments)
		{
			startInfo.ArgumentList.Add(argument);
		}

		using var process = Process.Start(startInfo);
		if (process is null)
		{
			return 1;
		}

		process.WaitForExit();
		return process.ExitCode;
	}
}
