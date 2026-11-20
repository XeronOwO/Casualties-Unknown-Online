using System.Globalization;
using System.Text.Json;

namespace CasualtiesUnknownOnline.AcceptanceHost;

/// <summary>
/// The update runbook's live half: compare a new batch's report with the stored baseline and print the
/// deltas, so a game update's behaviour change is named instead of felt. A regression exits 1: a baseline
/// row missing from the candidate, a row that stopped passing, a case that grew past max(5 s, 20% of its
/// baseline), the suite wall past max(10 s, 5%), or a case whose error count rose (warnings are declared
/// per case and printed, never counted). Two reports of different suites are refused as not comparable
/// (exit 2), and so is a file that is not a report or names no build.
/// </summary>
internal static class ReplayCommand
{
	private const string Usage =
		"replay --baseline <report.json> --candidate <report.json> [--case-wall-ms <n>] [--case-wall-percent <n>] " +
		"[--suite-wall-ms <n>] [--suite-wall-percent <n>]";

	public static int Run(string[] args)
	{
		var arguments = new Arguments(args, Usage);
		var caseFloorMs = arguments.OptionalNumber("case-wall-ms", 5000);
		var casePercent = arguments.OptionalNumber("case-wall-percent", 20);
		var suiteFloorMs = arguments.OptionalNumber("suite-wall-ms", 10000);
		var suitePercent = arguments.OptionalNumber("suite-wall-percent", 5);
		var baseline = Report.Read(arguments.Required("baseline"));
		var candidate = Report.Read(arguments.Required("candidate"));
		if (!string.Equals(baseline.Suite, candidate.Suite, StringComparison.Ordinal))
		{
			Console.Error.WriteLine(
				$"replay: not comparable — the baseline ran '{baseline.Suite}' and the candidate ran '{candidate.Suite}'");
			return 2;
		}

		var unstamped = new[] { baseline, candidate }.Where(report => report.Artifact == Report.Unstamped).ToArray();
		if (unstamped.Length > 0)
		{
			Console.Error.WriteLine(
				$"replay: not comparable — {unstamped[0].Path} names no build, so it cannot say which artifact produced its rows");
			return 2;
		}

		var regressions = 0;
		Console.WriteLine($"replay: suite '{baseline.Suite}'");
		Console.WriteLine($"baseline : {baseline.Path}");
		Console.WriteLine($"candidate: {candidate.Path}");
		Console.WriteLine($"build    : game {baseline.Game} -> {candidate.Game}; mod {baseline.Mod} -> {candidate.Mod}; artifact {baseline.Artifact} -> {candidate.Artifact}");

		var missing = baseline.Cases.Where(entry => !candidate.Cases.ContainsKey(entry.Key)).Select(entry => entry.Key).ToArray();
		var added = candidate.Cases.Keys.Where(name => !baseline.Cases.ContainsKey(name)).ToArray();
		Console.WriteLine($"row set  : {baseline.Cases.Count} -> {candidate.Cases.Count} rows; {missing.Length} missing, {added.Length} new");
		foreach (var name in missing)
		{
			Console.WriteLine($"  REGRESSION the baseline row '{name}' is not in the candidate");
			regressions++;
		}

		foreach (var name in added)
		{
			Console.WriteLine($"  new row '{name}: {candidate.Cases[name].Verdict}' — a row set that grew is reported, not a regression");
		}

		var suiteToleranceMs = Math.Max(suiteFloorMs, baseline.TotalMs * suitePercent / 100.0);
		var suiteDeltaMs = candidate.TotalMs - baseline.TotalMs;
		var suiteOver = suiteDeltaMs > suiteToleranceMs;
		Console.WriteLine(
			$"suite wall: {Milliseconds(baseline.TotalMs)} -> {Milliseconds(candidate.TotalMs)} ({Signed(suiteDeltaMs)} ms, tolerance {Milliseconds(suiteToleranceMs)}) {(suiteOver ? "REGRESSION" : "ok")}");
		if (suiteOver)
		{
			regressions++;
		}

		foreach (var entry in baseline.Cases)
		{
			if (!candidate.Cases.TryGetValue(entry.Key, out var now))
			{
				continue;
			}

			var before = entry.Value;
			var caseToleranceMs = Math.Max(caseFloorMs, before.CaseMs * casePercent / 100.0);
			var caseDeltaMs = now.CaseMs - before.CaseMs;
			var reasons = new List<string>();
			if (!string.Equals(before.Verdict, now.Verdict, StringComparison.Ordinal) && !string.Equals(now.Verdict, "PASS", StringComparison.Ordinal))
			{
				reasons.Add($"the verdict went {before.Verdict} -> {now.Verdict}");
			}

			if (caseDeltaMs > caseToleranceMs)
			{
				reasons.Add($"the case wall grew {Signed(caseDeltaMs)} ms past its {Milliseconds(caseToleranceMs)} ms tolerance");
			}

			// Warnings are declared and bounded by each case, and their count moves run to run: the 39b -> 39c
			// pair — a green improvement — moved ten of them, so only an error that appeared is a regression.
			foreach (var (label, was, isNow) in new[]
			{
				("host errors", before.HostErrors, now.HostErrors),
				("member errors", before.MemberErrors, now.MemberErrors),
			})
			{
				if (isNow > was)
				{
					reasons.Add($"{label} rose {was} -> {isNow}");
				}
			}

			var verdict = reasons.Count == 0 ? "ok" : "REGRESSION";
			Console.WriteLine(
				$"case {entry.Key}: {before.Verdict} -> {now.Verdict}; wall {Milliseconds(before.CaseMs)} -> {Milliseconds(now.CaseMs)} ({Signed(caseDeltaMs)} ms, tolerance {Milliseconds(caseToleranceMs)}) " +
				$"logs host {before.HostWarnings}/{before.HostErrors} -> {now.HostWarnings}/{now.HostErrors}, member {before.MemberWarnings}/{before.MemberErrors} -> {now.MemberWarnings}/{now.MemberErrors} {verdict}");
			foreach (var reason in reasons)
			{
				Console.WriteLine($"  REGRESSION ({entry.Key}) {reason}");
				regressions++;
			}
		}

		Console.WriteLine(regressions == 0 ? "result   : PASS — no regression" : $"result   : FAIL — {regressions} regression(s)");
		return regressions == 0 ? 0 : 1;
	}

	private static string Milliseconds(double value) => value.ToString("0.0", CultureInfo.InvariantCulture) + " ms";

	private static string Signed(double value) => (value >= 0 ? "+" : string.Empty) + value.ToString("0.0", CultureInfo.InvariantCulture);

	/// <summary>One report's row readings; the shape is <see cref="Suite"/>'s own JSON, never inferred from the Markdown.</summary>
	private sealed class Report
	{
		internal const string Unstamped = "unstamped";

		private Report(
			string path, string suite, string game, string mod, string artifact, double totalMs, Dictionary<string, CaseRow> cases)
		{
			Path = path;
			Suite = suite;
			Game = game;
			Mod = mod;
			Artifact = artifact;
			TotalMs = totalMs;
			Cases = cases;
		}

		internal string Path { get; }

		internal string Suite { get; }

		internal string Game { get; }

		internal string Mod { get; }

		internal string Artifact { get; }

		internal double TotalMs { get; }

		internal Dictionary<string, CaseRow> Cases { get; }

		internal static Report Read(string path)
		{
			if (!File.Exists(path))
			{
				throw new CommandFailure(2, $"no report at {path}");
			}

			using var document = JsonDocument.Parse(File.ReadAllText(path));
			var root = document.RootElement;
			try
			{
				var cases = new Dictionary<string, CaseRow>(StringComparer.Ordinal);
				foreach (var item in root.GetProperty("cases").EnumerateArray())
				{
					var name = item.GetProperty("name").GetString() ?? string.Empty;
					cases[name] = new CaseRow(
						Text(item, "verdict"),
						Number(item, "caseMs"),
						Counter(item, "hostWarnings"),
						Counter(item, "hostErrors"),
						Counter(item, "memberWarnings"),
						Counter(item, "memberErrors"));
				}

				var stamped = root.TryGetProperty("build", out var build) && build.ValueKind == JsonValueKind.Object;
				return new Report(
					path,
					Text(root, "suite"),
					stamped ? Text(build, "game") : Unstamped,
					stamped ? Text(build, "mod") : Unstamped,
					stamped ? Text(build, "artifact") : Unstamped,
					Number(root, "totalMs"),
					cases);
			}
			catch (Exception exception) when (exception is KeyNotFoundException or InvalidOperationException or FormatException)
			{
				throw new CommandFailure(2, $"{path} is not a suite report: {exception.Message}");
			}
		}

		private static string Text(JsonElement element, string property) =>
			element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
				? value.GetString() ?? string.Empty
				: string.Empty;

		private static double Number(JsonElement element, string property) =>
			element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
				? value.GetDouble()
				: throw new FormatException($"'{property}' is not a number");

		private static int Counter(JsonElement element, string property) =>
			element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
				? value.GetInt32()
				: 0;
	}

	private sealed record CaseRow(string Verdict, double CaseMs, int HostWarnings, int HostErrors, int MemberWarnings, int MemberErrors);
}
