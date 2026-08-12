using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The event-replay matrix's data integrity (5ccec0e — the audit that caught
/// three replay gaps at once; the CSV is the per-mechanism audit the
/// check-event-replay gate consumes). The data becoming a test: every row
/// must be complete and consistent with the kind archive — a new kind needs
/// its audited row, a covered mechanism needs all five effect columns, a
/// row that cannot be parsed fails instead of silently weakening the gate.
/// </summary>
public class ReplayMatrixDataTests
{
	private static readonly string MatrixPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "event-replay-matrix.csv");

	private static readonly string[] EffectColumns = ["sound-trigger", "sound-replay", "visual-trigger", "visual-replay", "state-consumption"];

	private static string[] ParseRow(string line)
	{
		// Quote-aware split: a quoted field may contain commas. The current
		// matrix avoids commas in values, but a future row must not silently
		// break the audit.
		var fields = new List<string>();
		var current = string.Empty;
		var inQuotes = false;
		foreach (var ch in line)
		{
			if (ch == '"')
			{
				inQuotes = !inQuotes;
			}
			else if (ch == ',' && !inQuotes)
			{
				fields.Add(current);
				current = string.Empty;
			}
			else
			{
				current += ch;
			}
		}

		fields.Add(current);
		return [.. fields];
	}

	private static (string Kind, string Status, string[] Effects)[] LoadRows()
	{
		if (!File.Exists(MatrixPath))
		{
			Assert.True(false, "event-replay-matrix.csv missing from the test output (csproj None copy) — the audit data gate cannot run");
		}

		var rows = new List<(string Kind, string Status, string[] Effects)>();
		foreach (var raw in File.ReadAllLines(MatrixPath))
		{
			var line = raw.Trim();
			if (line.Length == 0 || line.StartsWith("kind,"))
			{
				continue; // blank lines + the header
			}

			var fields = ParseRow(line);
			Assert.True(fields.Length == 8, $"expected 8 columns, got {fields.Length}: '{string.Join(",", fields)}'");
			rows.Add((fields[0], fields[6], [fields[1], fields[2], fields[3], fields[4], fields[5]]));
		}

		return [.. rows];
	}

	[Fact]
	public void EveryKindHasExactlyOneAuditedRow()
	{
		var rows = LoadRows();
		foreach (var kind in EntityEventArchives.AllKinds)
		{
			Assert.True(rows.Count(r => r.Kind == kind.ToString()) == 1,
				$"{kind}: exactly one audited row required (got {rows.Count(r => r.Kind == kind.ToString())})");
		}

		Assert.True(rows.Length == EntityEventArchives.AllKinds.Count(),
			$"the matrix must cover exactly the kind archive ({EntityEventArchives.AllKinds.Count()} kinds), got {rows.Length} rows");
	}

	[Fact]
	public void StatusIsDeclared()
	{
		var unknown = LoadRows().Where(r => r.Status != "covered" && r.Status != "excluded").Select(r => r.Kind).ToList();
		Assert.True(unknown.Count == 0,
			$"every row's status must be covered or excluded, got [{string.Join(", ", unknown)}]");
	}

	[Fact]
	public void CoveredRows_HaveEveryEffectColumnFilled()
	{
		var gaps = new List<string>();
		foreach (var row in LoadRows().Where(r => r.Status == "covered"))
		{
			for (var i = 0; i < EffectColumns.Length; i++)
			{
				if (row.Effects[i].Trim().Length == 0)
				{
					gaps.Add($"{row.Kind}: '{EffectColumns[i]}' is empty");
				}
			}
		}

		Assert.True(gaps.Count == 0, $"a covered mechanism needs all five effect columns ({gaps.Count} gaps):\n" + string.Join("\n", gaps));
	}

	[Fact]
	public void ExcludedRows_RecordTheReasonInNotes()
	{
		var gaps = LoadRows().Where(r => r.Status == "excluded" && r.Effects[0].Trim().Length == 0).ToList();
		Assert.True(gaps.Count == 0, $"an excluded row must carry its reason in the effect column ('each side's own body' etc.)");
	}
}
