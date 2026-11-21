using System.Globalization;
using System.Text;

namespace CasualtiesUnknownOnline.AcceptanceSuite.Cases;

/// <summary>
/// The capability report on the deployed build is complete and honest: every catalog
/// capability appears once with its Required/Optional class and its contract count, the
/// report's own count line agrees with the rows, the install line names the targets it
/// verified, and the verdict says the session is available. A row that reads FAILED, a
/// missing capability or a count mismatch is a finding — this is the one place a run
/// states what the adapter thinks of the game it is attached to.
///
/// It reads the boot log, so it costs the run nothing and it is the only row that can fail
/// when an update breaks a contract before any world exists.
/// </summary>
internal sealed class CapabilityReportCase : AcceptanceCase
{
	public override string Name => "capability-report";

	public override CaseCost Cost => CaseCost.InRun;


	private static readonly string[] ExpectedIds =
	[
		"session", "world", "items", "traps", "medical", "character", "enemies", "crafting",
		"trading", "save", "tutorial", "mods", "diagnostics",
	];

	public override async Task RunAsync(CaseContext context, CancellationToken cancellationToken)
	{
		var log = context.HostLogPath;
		var text = ReadLog(log);
		var reportStart = await WaitForReportAsync(log, cancellationToken).ConfigureAwait(false);
		var report = text.Substring(reportStart);
		var boot = text[..reportStart];

		var rows = report
			.Split('\n')
			.Where(line => line.Contains("[Required] ", StringComparison.Ordinal) || line.Contains("[Optional] ", StringComparison.Ordinal))
			.Select(line => line.TrimEnd('\r'))
			.ToList();

		Require(rows.Count == ExpectedIds.Length,
			"the capability report does not list every capability: " + rows.Count.ToString(CultureInfo.InvariantCulture) + " row(s) for " + ExpectedIds.Length.ToString(CultureInfo.InvariantCulture) + " expected; report: " + Raw(rows));

		foreach (var id in ExpectedIds)
		{
			var matching = rows.Where(row => row.Contains("] " + id + " —", StringComparison.Ordinal)).ToList();
			Require(matching.Count == 1,
				"the capability report has " + matching.Count.ToString(CultureInfo.InvariantCulture) + " row(s) for '" + id + "'; report: " + Raw(rows));
			var row = matching[0];
			Require(!row.Contains("FAILED", StringComparison.Ordinal),
				"the capability report names a broken capability on a healthy build: " + row);
			Require(row.Contains("contract(s)", StringComparison.Ordinal),
				"the capability row does not state its contract count: " + row);
		}

		var countLine = rows.Count.ToString(CultureInfo.InvariantCulture) + " capability(ies)";
		Require(report.Contains(countLine, StringComparison.Ordinal),
			"the report's count line does not name " + countLine + "; report: " + Raw(rows));

		Require(boot.Contains("Game Adapter patches installed and verified", StringComparison.Ordinal),
			"the boot log carries no install-and-verify line before the report");
		Require(report.Contains("verdict: session available", StringComparison.Ordinal),
			"the report's verdict does not say the session is available; report: " + Raw(rows));
		Require(!report.Contains("verdict: session REFUSED", StringComparison.Ordinal),
			"the report refuses the session on a healthy build");
	}

	/// <summary>The offset of the report's own header in the log, or a failure when the boot log has none.</summary>
	private static async Task<int> WaitForReportAsync(string path, CancellationToken cancellationToken)
	{
		var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
		while (DateTime.UtcNow < deadline)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var text = ReadLog(path);
			var index = text.LastIndexOf("Game Adapter capability report:", StringComparison.Ordinal);
			if (index >= 0)
			{
				return index;
			}

			await Task.Delay(200, cancellationToken).ConfigureAwait(false);
		}

		throw new InvalidOperationException("the host log carries no capability report within 30 s: " + path);
	}

	private static string ReadLog(string path)
	{
		try
		{
			using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
			using var reader = new StreamReader(stream, Encoding.UTF8);
			return reader.ReadToEnd();
		}
		catch (IOException)
		{
			return string.Empty;
		}
	}

	private static string Raw(IEnumerable<string> rows) =>
		string.Join(" | ", rows.Select(row => row.Length > 160 ? row[..160] : row));

	private static void Require(bool condition, string message)
	{
		if (!condition)
		{
			throw new InvalidOperationException(message);
		}
	}
}
