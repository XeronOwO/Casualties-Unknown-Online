using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// Runs every existing <c>tools/check-*.ps1</c> gate from the ordinary
/// <c>dotnet test</c> loop. The scripts remain the canonical implementation for
/// repo-wide/data/process checks; this wrapper makes them part of the unit-test
/// run instead of leaving them as optional PowerShell-only gates.
/// </summary>
public class ExistingPowerShellGateTests
{
	[Fact]
	public void AllExistingCheckScripts_Pass()
	{
		var root = FindRepositoryRoot();
		var failures = new List<string>();

		foreach (var script in Directory.EnumerateFiles(Path.Combine(root, "tools"), "check-*.ps1").OrderBy(p => p, StringComparer.Ordinal))
		{
			var startInfo = new ProcessStartInfo
			{
				FileName = "powershell.exe",
				Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\"",
				WorkingDirectory = root,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true
			};

			using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"failed to start powershell.exe for {Path.GetFileName(script)}");
			var output = process.StandardOutput.ReadToEnd();
			var error = process.StandardError.ReadToEnd();
			process.WaitForExit();

			if (process.ExitCode != 0)
			{
				failures.Add($"=== {Path.GetFileName(script)} ===\n{output}{error}");
			}
		}

		Assert.True(failures.Count == 0,
			"PowerShell check gates failed" + Environment.NewLine + string.Join(Environment.NewLine, failures));
	}

	private static string FindRepositoryRoot()
	{
		var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
		while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CasualtiesUnknownOnline.slnx")))
		{
			directory = directory.Parent;
		}

		Assert.True(directory is not null, "could not locate repository root (CasualtiesUnknownOnline.slnx)");
		return directory!.FullName;
	}
}
