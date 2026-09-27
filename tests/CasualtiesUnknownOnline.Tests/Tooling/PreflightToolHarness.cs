using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace CasualtiesUnknownOnline.Tests.Tooling;

/// <summary>
/// The black-box harness for tools/acceptance/preflight.ps1: a throwaway acceptance machine (fact file,
/// dummy executables and a fake Steam install) plus one Windows PowerShell invocation per call. Every
/// call spawns its own process and fixture directory — this class holds no state.
/// </summary>
internal static class PreflightToolHarness
{
	internal static RunResult Run(Fixture fixture)
	{
		var script = FindScript();
		var arguments = string.Join(" ", ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", Quote(script), "-FactsPath", Quote(fixture.FactsPath)]);

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
		return new RunResult(process.ExitCode, stderr.Length == 0 ? stdout : stdout + Environment.NewLine + "[stderr] " + stderr);
	}

	internal static Row ReadRow(string output, string id)
	{
		var match = Regex.Match(output, "^\\[(?<state>present|missing|unknown|pending)\\]\\s+" + Regex.Escape(id) + "\\s+(?<detail>.*)$", RegexOptions.Multiline);
		if (!match.Success)
		{
			throw new InvalidOperationException($"no '{id}' row in the preflight output:{Environment.NewLine}{output}");
		}

		return new Row(match.Groups["state"].Value, match.Groups["detail"].Value.Trim());
	}

	internal static string BlockingIds(string output)
	{
		var match = Regex.Match(output, "^RESULT: MISSING - (?<ids>[^;]+);", RegexOptions.Multiline);
		return match.Success ? match.Groups["ids"].Value : string.Empty;
	}

	private static string FindScript()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory != null)
		{
			var candidate = Path.Combine(directory.FullName, "tools", "acceptance", "preflight.ps1");
			if (File.Exists(candidate))
			{
				return candidate;
			}

			directory = directory.Parent;
		}

		throw new InvalidOperationException($"tools/acceptance/preflight.ps1 not found above {AppContext.BaseDirectory}");
	}

	private static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";

	internal sealed record RunResult(int ExitCode, string Output);

	internal sealed record Row(string State, string Detail);

	/// <summary>
	/// A throwaway acceptance machine: facts pointing at dummy executables, and the install under
	/// &lt;root&gt;\library\steamapps\common\&lt;install name&gt; so both cross-check sources can be staged.
	/// </summary>
	internal sealed class Fixture : IDisposable
	{
		internal const string InstallName = "Casualties Unknown Demo";

		private Fixture(string root)
		{
			Root = root;
		}

		internal string Root { get; }

		internal string GameDir => Path.Combine(Root, "library", "steamapps", "common", InstallName);

		internal string FactsPath => Path.Combine(Root, "facts.md");

		internal static Fixture Create(string gameAppId)
		{
			var root = Path.Combine(Path.GetTempPath(), "cuo-preflight-tests", Guid.NewGuid().ToString("N"));
			var fixture = new Fixture(root);
			Directory.CreateDirectory(fixture.GameDir);
			Directory.CreateDirectory(Path.Combine(root, "sandbox"));
			Directory.CreateDirectory(Path.Combine(root, "artifacts"));
			File.WriteAllText(Path.Combine(root, "steam.exe"), string.Empty);
			File.WriteAllText(Path.Combine(root, "sandboxie.exe"), string.Empty);
			fixture.WriteFacts(gameAppId);
			return fixture;
		}

		internal Fixture WithInstallAppId(string value)
		{
			File.WriteAllText(Path.Combine(GameDir, "steam_appid.txt"), value, Utf8NoBom);
			return this;
		}

		internal Fixture WithAppManifest(string manifestAppId, string name, string installDir)
		{
			string[] lines =
			[
				"\"AppState\"",
				"{",
				"\t\"appid\"\t\t\"" + manifestAppId + "\"",
				"\t\"name\"\t\t\"" + name + "\"",
				"\t\"installdir\"\t\t\"" + installDir + "\"",
				"}",
			];
			File.WriteAllText(Path.Combine(Path.Combine(Root, "library", "steamapps"), "appmanifest_" + manifestAppId + ".acf"), string.Join(Environment.NewLine, lines) + Environment.NewLine, Utf8NoBom);
			return this;
		}

		private static UTF8Encoding Utf8NoBom { get; } = new(encoderShouldEmitUTF8Identifier: false);

		private void WriteFacts(string gameAppId)
		{
			string[] lines =
			[
				"## acceptance environment",
				"- game-dir: " + GameDir,
				"- game-app-id: " + gameAppId,
				"- steam-exe: " + Path.Combine(Root, "steam.exe"),
				"- sandboxie-exe: " + Path.Combine(Root, "sandboxie.exe"),
				"- sandbox-guest-root: " + Path.Combine(Root, "sandbox"),
				"- acceptance-artifacts-dir: " + Path.Combine(Root, "artifacts"),
			];
			File.WriteAllText(FactsPath, string.Join(Environment.NewLine, lines) + Environment.NewLine, Utf8NoBom);
		}

		public void Dispose() => Directory.Delete(Root, recursive: true);
	}
}
