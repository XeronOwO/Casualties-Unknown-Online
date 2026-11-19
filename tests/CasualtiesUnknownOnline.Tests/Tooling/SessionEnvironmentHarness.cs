using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace CasualtiesUnknownOnline.Tests.Tooling;

/// <summary>
/// The black-box harness for the acceptance host's <c>env</c> command (the C# port of the deleted
/// <c>session-environment.ps1</c>): a throwaway game install whose BepInEx trees are staged by marker DLL,
/// plus one host invocation per call. Every call spawns its own process; a fixture owns its directory and
/// deletes it.
/// </summary>
internal static class SessionEnvironmentHarness
{
	private static readonly Lazy<string> HostExecutable = new(FindHostExecutable);

	internal static RunResult Run(Fixture fixture, params string[] arguments) => Run(fixture.GameDir, arguments);

	internal static RunResult Run(string gameDir, params string[] arguments)
	{
		var parts = new List<string> { "env", "--game", Quote(gameDir) };
		parts.AddRange(Translate(arguments));

		var startInfo = new ProcessStartInfo(HostExecutable.Value, string.Join(" ", parts))
		{
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			StandardOutputEncoding = Encoding.UTF8,
			StandardErrorEncoding = Encoding.UTF8,
			CreateNoWindow = true,
		};

		using var process = Process.Start(startInfo)
			?? throw new InvalidOperationException("the acceptance host did not start");
		var stdout = process.StandardOutput.ReadToEnd();
		var stderr = process.StandardError.ReadToEnd();
		process.WaitForExit();
		return new RunResult(process.ExitCode, stderr.Length == 0 ? stdout : stdout + Environment.NewLine + "[stderr] " + stderr);
	}

	/// <summary>
	/// Finds the host project above the test output and makes sure its executable exists (builds once when
	/// the developer has not built it). The host is net8 and the tests are net48, so it is always a child
	/// process, never a reference.
	/// </summary>
	private static string FindHostExecutable()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory != null)
		{
			var project = Path.Combine(directory.FullName, "tools", "acceptance", "host", "CasualtiesUnknownOnline.AcceptanceHost.csproj");
			if (File.Exists(project))
			{
				var executable = FindBuiltExecutable(Path.GetDirectoryName(project)!);
				if (executable is not null)
				{
					return executable;
				}

				BuildHost(project);
				return FindBuiltExecutable(Path.GetDirectoryName(project)!)
					?? throw new InvalidOperationException($"the acceptance host did not build next to {project}");
			}

			directory = directory.Parent;
		}

		throw new InvalidOperationException($"tools/acceptance/host not found above {AppContext.BaseDirectory}");
	}

	private static string? FindBuiltExecutable(string projectDirectory)
	{
		foreach (var configuration in new[] { "Debug", "Release" })
		{
			var candidate = Path.Combine(projectDirectory, "bin", configuration, "net8.0-windows", "CasualtiesUnknownOnline.AcceptanceHost.exe");
			if (File.Exists(candidate))
			{
				return candidate;
			}
		}

		return null;
	}

	private static void BuildHost(string project)
	{
		var startInfo = new ProcessStartInfo("dotnet", $"build \"{project}\" --nologo -nodeReuse:false -p:UseSharedCompilation=false")
		{
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			CreateNoWindow = true,
		};
		using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("dotnet did not start");
		process.StandardOutput.ReadToEnd();
		process.StandardError.ReadToEnd();
		process.WaitForExit();
		if (process.ExitCode != 0)
		{
			throw new InvalidOperationException($"dotnet build failed for {project}");
		}
	}

	/// <summary>
	/// The tests were written against the PowerShell script's <c>-Name value</c> arguments; the host reads
	/// <c>--name value</c>. One mapping keeps every call site and its assertion unchanged.
	/// </summary>
	private static IEnumerable<string> Translate(string[] arguments)
	{
		for (var index = 0; index < arguments.Length; index++)
		{
			var argument = arguments[index];
			if (argument.Length == 0 || argument[0] != '-' || argument.StartsWith("--", StringComparison.Ordinal))
			{
				yield return argument;
				continue;
			}

			var trimmed = argument.Substring(1);
			var name = trimmed switch
			{
				"GameProcessName" => "process",
				"PlayMarkerName" => "play-marker",
				"ParkedTreeName" => "parked-tree",
				"Mode" => "mode",
				"GameDir" => "game",
				_ => trimmed.ToLowerInvariant(),
			};
			yield return "--" + name;
		}
	}

	private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";

	internal sealed record RunResult(int ExitCode, string Output);

	/// <summary>
	/// A throwaway game install: the ACTIVE tree is always <c>&lt;game&gt;\BepInEx</c>, and the staged
	/// marker DLLs decide what each tree is — no folder name carries meaning. The markers are neutral
	/// stand-ins: on a real machine both names are facts of that machine, not of this repository.
	/// </summary>
	internal sealed class Fixture : IDisposable
	{
		/// <summary>CUO's own marker (the script's default) — this repository's DLL name.</summary>
		internal const string CuoMarkerName = "CasualtiesUnknownOnline.dll";

		/// <summary>The stand-in for whatever DLL marks the machine owner's play tree.</summary>
		internal const string OtherMarkerName = "play-marker.dll";

		internal const string CuoMarkerPath = "plugins/CasualtiesUnknownOnline/CasualtiesUnknownOnline.dll";

		internal const string OtherMarkerPath = "plugins/owner-mod/play-marker.dll";

		private Fixture(string root)
		{
			Root = root;
		}

		internal string Root { get; }

		internal string GameDir => Path.Combine(Root, "game");

		/// <summary>The owner is playing: their tree is ACTIVE and CUO's is parked under a name nobody
		/// promises (the real machine renames it by hand).</summary>
		internal static Fixture WithTheOwnerPlaying() =>
			Create().Stage("BepInEx", OtherMarkerPath).Stage("BepInEx-cuo", CuoMarkerPath);

		/// <summary>The run's own layout: CUO is ACTIVE and the play tree is parked.</summary>
		internal static Fixture WithCuoActive() =>
			Create().Stage("BepInEx", CuoMarkerPath).Stage("BepInEx-play", OtherMarkerPath);

		/// <summary>A play tree is ACTIVE and no sibling carries the CUO marker.</summary>
		internal static Fixture WithoutACuoTree() =>
			Create().Stage("BepInEx", OtherMarkerPath);

		internal bool TreeExists(string name) => Directory.Exists(Path.Combine(GameDir, name));

		internal bool MarkerExists(string tree, string markerPath) =>
			File.Exists(Path.Combine(GameDir, tree, markerPath.Replace('/', Path.DirectorySeparatorChar)));

		private static Fixture Create()
		{
			var root = Path.Combine(Path.GetTempPath(), "cuo-session-environment-tests", Guid.NewGuid().ToString("N"));
			var fixture = new Fixture(root);
			Directory.CreateDirectory(fixture.GameDir);
			return fixture;
		}

		private Fixture Stage(string tree, string markerPath)
		{
			var path = Path.Combine(GameDir, tree, markerPath.Replace('/', Path.DirectorySeparatorChar));
			Directory.CreateDirectory(Path.GetDirectoryName(path)!);
			File.WriteAllText(path, string.Empty);
			return this;
		}

		public void Dispose() => Directory.Delete(Root, recursive: true);
	}
}
