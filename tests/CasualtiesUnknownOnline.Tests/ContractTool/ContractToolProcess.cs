using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace CasualtiesUnknownOnline.Tests.ContractTool;

/// <summary>
/// Runs the built tool as a real process. The update-day runbook calls the
/// executable, so the tests that pin the CLI contract go through the same door:
/// arguments in, exit code and streams out — no library shortcut.
/// </summary>
internal static class ContractToolProcess
{
	/// <summary>
	/// The tool's own output folder beside the test output: the process must run with
	/// ITS dependency set as the application base (the test output carries different
	/// System.Memory / System.Runtime.CompilerServices.Unsafe versions, and probing
	/// uses the app base, not the working directory). See the csproj copy target.
	/// </summary>
	private static readonly string ToolPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "contracttool", "CasualtiesUnknownOnline.ContractTool.exe");

	/// <summary>A hung tool must fail the test suite loudly instead of hanging it.</summary>
	private const int TimeoutMs = 120_000;

	internal static (int ExitCode, string StandardOutput, string StandardError) Run(params string[] arguments)
	{
		var startInfo = new ProcessStartInfo(ToolPath)
		{
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true,
			Arguments = string.Join(" ", arguments.Select(argument => "\"" + argument.Replace("\"", "\\\"") + "\"")),
		};

		using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"could not start {ToolPath}");
		// Both streams are drained concurrently: reading one to EOF first deadlocks when
		// the other fills its pipe buffer.
		var standardOutput = process.StandardOutput.ReadToEndAsync();
		var standardError = process.StandardError.ReadToEndAsync();
		if (!process.WaitForExit(TimeoutMs))
		{
			process.Kill();
			throw new InvalidOperationException($"{ToolPath} did not exit within {TimeoutMs} ms");
		}

		return (process.ExitCode, standardOutput.Result, standardError.Result);
	}
}
