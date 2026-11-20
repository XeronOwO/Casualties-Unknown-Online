using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling;

/// <summary>
/// The update runbook's live half, <c>host replay</c>: a new batch's report is compared with the stored
/// baseline and the deltas are printed, so a game update's behaviour change is named instead of felt. The
/// rows pinned here are the comparer's own refusals — a within-tolerance pair passes, and every regression
/// it claims to catch (a lost row, a verdict that stopped passing, a case wall past max(5 s, 20%), an error
/// that appeared) exits 1; two different suites, a report that names no build, and a file that is not a
/// report exit 2.
/// </summary>
public class ReplayToolTests
{
	[Fact]
	public void GreenPair_WithinTolerance_Passes()
	{
		using var fixture = Fixture.Create();
		var baseline = fixture.Write("baseline.json", Report("all", 262100, Row("save-continue", "PASS", 36500), Row("world-fingerprint", "PASS", 2300)));
		var candidate = fixture.Write("candidate.json", Report("all", 265000, Row("save-continue", "PASS", 37000), Row("world-fingerprint", "PASS", 2500)));

		var result = Run(baseline, candidate);

		Assert.Equal(0, result.ExitCode);
		Assert.Contains("result   : PASS", result.Output);
		Assert.Contains("suite wall", result.Output);
	}

	[Fact]
	public void RowMissingFromTheCandidate_IsARegression()
	{
		using var fixture = Fixture.Create();
		var baseline = fixture.Write("baseline.json", Report("all", 262100, Row("save-continue", "PASS", 36500), Row("world-fingerprint", "PASS", 2300)));
		var candidate = fixture.Write("candidate.json", Report("all", 262100, Row("save-continue", "PASS", 36500)));

		var result = Run(baseline, candidate);

		Assert.Equal(1, result.ExitCode);
		Assert.Contains("the baseline row 'world-fingerprint' is not in the candidate", result.Output);
		Assert.Contains("result   : FAIL", result.Output);
	}

	[Fact]
	public void PassBecomesFail_IsARegression()
	{
		using var fixture = Fixture.Create();
		var baseline = fixture.Write("baseline.json", Report("all", 262100, Row("save-continue", "PASS", 36500)));
		var candidate = fixture.Write("candidate.json", Report("all", 262100, Row("save-continue", "FAIL", 36000)));

		var result = Run(baseline, candidate);

		Assert.Equal(1, result.ExitCode);
		Assert.Contains("the verdict went PASS -> FAIL", result.Output);
	}

	[Fact]
	public void CaseWallPastTheTolerance_IsARegression()
	{
		using var fixture = Fixture.Create();
		var baseline = fixture.Write("baseline.json", Report("all", 262100, Row("save-continue", "PASS", 36500)));
		var candidate = fixture.Write("candidate.json", Report("all", 262100, Row("save-continue", "PASS", 90000)));

		var result = Run(baseline, candidate);

		Assert.Equal(1, result.ExitCode);
		Assert.Contains("the case wall grew", result.Output);
	}

	[Fact]
	public void ErrorCountRose_IsARegression()
	{
		using var fixture = Fixture.Create();
		var baseline = fixture.Write("baseline.json", Report("all", 262100, Row("save-continue", "PASS", 36500, hostErrors: 0)));
		var candidate = fixture.Write("candidate.json", Report("all", 262100, Row("save-continue", "PASS", 36500, hostErrors: 1)));

		var result = Run(baseline, candidate);

		Assert.Equal(1, result.ExitCode);
		Assert.Contains("host errors rose 0 -> 1", result.Output);
	}

	[Fact]
	public void ReportWithoutABuild_IsRefused()
	{
		using var fixture = Fixture.Create();
		var baseline = fixture.Write("baseline.json", ReportWithoutBuild("all", 262100, Row("save-continue", "PASS", 36500)));
		var candidate = fixture.Write("candidate.json", Report("all", 262100, Row("save-continue", "PASS", 36500)));

		var result = Run(baseline, candidate);

		Assert.Equal(2, result.ExitCode);
		Assert.Contains("names no build", result.Output);
	}

	[Fact]
	public void DifferentSuites_AreRefused()
	{
		using var fixture = Fixture.Create();
		var baseline = fixture.Write("baseline.json", Report("all", 262100, Row("save-continue", "PASS", 36500)));
		var candidate = fixture.Write("candidate.json", Report("save-continue-reentry", 90000, Row("save-continue", "PASS", 36500)));

		var result = Run(baseline, candidate);

		Assert.Equal(2, result.ExitCode);
		Assert.Contains("not comparable", result.Output);
	}

	private static (int ExitCode, string Output) Run(string baseline, string candidate)
	{
		var host = FindHost();
		var arguments = string.Join(" ", Quote(host), "replay", "--baseline", Quote(baseline), "--candidate", Quote(candidate));
		var startInfo = new ProcessStartInfo("dotnet", arguments)
		{
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			StandardOutputEncoding = Encoding.UTF8,
			StandardErrorEncoding = Encoding.UTF8,
			CreateNoWindow = true,
		};

		using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("dotnet did not start");
		var output = process.StandardOutput.ReadToEnd();
		var error = process.StandardError.ReadToEnd();
		process.WaitForExit();
		return (process.ExitCode, error.Length == 0 ? output : output + Environment.NewLine + "[stderr] " + error);
	}

	private static string FindHost()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory != null)
		{
			var candidate = Path.Combine(directory.FullName, "tools", "acceptance", "host", "bin", "Debug", "net8.0-windows", "CasualtiesUnknownOnline.AcceptanceHost.dll");
			if (File.Exists(candidate))
			{
				return candidate;
			}

			directory = directory.Parent;
		}

		throw new InvalidOperationException($"the acceptance host's build was not found above {AppContext.BaseDirectory}");
	}

	private static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";

	private static string Report(string suite, double totalMs, params string[] rows) => string.Join(
		Environment.NewLine,
		"{",
		"  \"suite\": \"" + suite + "\",",
		"  \"build\": { \"game\": \"1.0.2\", \"mod\": \"0.1.0+abc\", \"artifact\": \"0.1.0+abc\" },",
		"  \"totalMs\": " + totalMs.ToString(CultureInfo.InvariantCulture) + ",",
		"  \"cases\": [",
		string.Join("," + Environment.NewLine, Array.ConvertAll(rows, row => "    " + row)),
		"  ]",
		"}");

	private static string ReportWithoutBuild(string suite, double totalMs, params string[] rows) => string.Join(
		Environment.NewLine,
		"{",
		"  \"suite\": \"" + suite + "\",",
		"  \"totalMs\": " + totalMs.ToString(CultureInfo.InvariantCulture) + ",",
		"  \"cases\": [",
		string.Join("," + Environment.NewLine, Array.ConvertAll(rows, row => "    " + row)),
		"  ]",
		"}");

	private static string Row(string name, string verdict, double caseMs, int hostErrors = 0) => string.Join(
		" ",
		"{",
		"\"name\": \"" + name + "\",",
		"\"verdict\": \"" + verdict + "\",",
		"\"caseMs\": " + caseMs.ToString(CultureInfo.InvariantCulture) + ",",
		"\"hostWarnings\": 0,",
		"\"hostErrors\": " + hostErrors.ToString(CultureInfo.InvariantCulture) + ",",
		"\"memberWarnings\": 0,",
		"\"memberErrors\": 0",
		"}");

	private sealed class Fixture : IDisposable
	{
		private Fixture(string root)
		{
			Root = root;
		}

		internal string Root { get; }

		internal static Fixture Create()
		{
			var root = Path.Combine(Path.GetTempPath(), "cuo-replay-tests", Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(root);
			return new Fixture(root);
		}

		internal string Write(string name, string content)
		{
			var path = Path.Combine(Root, name);
			File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			return path;
		}

		public void Dispose() => Directory.Delete(Root, recursive: true);
	}
}
