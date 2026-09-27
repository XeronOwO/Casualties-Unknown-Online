using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace CasualtiesUnknownOnline.Tests.Tooling;

/// <summary>
/// The black-box harness for tools/acceptance/drive-in-process.ps1: one Windows PowerShell invocation
/// per call, against whatever endpoint the caller points it at. Every call spawns its own process and
/// this class holds no state.
/// </summary>
internal static class DriverToolHarness
{
	internal static RunResult Run(params string[] arguments)
	{
		var script = FindScript();
		var parts = new List<string> { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", Quote(script) };
		parts.AddRange(arguments);

		var startInfo = new ProcessStartInfo("powershell.exe", string.Join(" ", parts))
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

	internal static string FindScript()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory != null)
		{
			var candidate = Path.Combine(directory.FullName, "tools", "acceptance", "drive-in-process.ps1");
			if (File.Exists(candidate))
			{
				return candidate;
			}

			directory = directory.Parent;
		}

		throw new InvalidOperationException($"tools/acceptance/drive-in-process.ps1 not found above {AppContext.BaseDirectory}");
	}

	internal static string FindTemplate() =>
		Path.Combine(Path.GetDirectoryName(FindScript())!, "driver", "InProcessDriver.cs");

	/// <summary>Reads the first JSON field with the given name; the driver's own output is pretty-printed, so the pattern tolerates spacing and value kinds.</summary>
	internal static string Field(string json, string name)
	{
		var match = Regex.Match(json, "\"" + Regex.Escape(name) + "\"\\s*:\\s*\"?(?<value>[^\",}\\r\\n]*)");
		if (!match.Success)
		{
			throw new InvalidOperationException($"no '{name}' field in:{Environment.NewLine}{json}");
		}

		return match.Groups["value"].Value.Trim();
	}

	/// <summary>True when the JSON carries the named field at all (a stronger check than a bare substring search for a field name).</summary>
	internal static bool HasField(string json, string name) =>
		Regex.IsMatch(json, "\"" + Regex.Escape(name) + "\"\\s*:");

	private static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";

	internal sealed record RunResult(int ExitCode, string Output);
}
