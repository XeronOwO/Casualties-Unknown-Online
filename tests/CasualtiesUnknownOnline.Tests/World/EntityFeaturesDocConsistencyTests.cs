using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The narrative entity tables must stay aligned with the matrix on the columns
/// that are completeness facts: every narrative row names a matrix entity, every
/// matrix entity appears exactly once, and each row's sync cell carries the
/// matrix's verdict. The narrative lives in the entity tables of
/// docs/en/reference/feature-matrices.md, which the csproj copies into the test
/// output; this test makes a drift between that page and the CSV a `dotnet test`
/// failure instead of a documentation review finding. A table without an `entity`
/// column is not an entity narrative table (the same page also carries item tables
/// and the column keys) and is skipped; a table that carries `entity` without
/// `sync` and `path` is a failure. The path cell is checked for being filled, not
/// for matching the CSV text: the page names the covering mechanism in its own
/// words, and the CSV keeps the single home of the machine wording.
/// </summary>
public class EntityFeaturesDocConsistencyTests
{
	private static readonly string BaseDir = AppDomain.CurrentDomain.BaseDirectory;

	private static readonly string MatrixPath = Path.Combine(BaseDir, "entity-features-matrix.csv");

	private static readonly string DocPath = Path.Combine(BaseDir, "feature-matrices.md");

	private sealed record MatrixRow(string Entity, string Sync, string Path);

	private sealed record MarkdownTable(string[] Header, List<string[]> Rows);

	private static Dictionary<string, MatrixRow> LoadMatrix()
	{
		Assert.True(File.Exists(MatrixPath),
			"entity-features-matrix.csv missing from the test output (csproj None copy) — the doc-consistency gate cannot run.");

		var lines = File.ReadAllLines(MatrixPath);
		Assert.True(lines.Length >= 2, "entity-features-matrix.csv needs a header and at least one data row.");

		var header = ParseCsvLine(lines[0]);
		var entityIdx = ColumnIndex(header, "entity");
		var syncIdx = ColumnIndex(header, "sync");
		var pathIdx = ColumnIndex(header, "path");
		Assert.True(entityIdx >= 0 && syncIdx >= 0 && pathIdx >= 0,
			"entity-features-matrix.csv must keep the entity/sync/path columns.");

		var rows = new Dictionary<string, MatrixRow>(StringComparer.Ordinal);
		for (var i = 1; i < lines.Length; i++)
		{
			if (string.IsNullOrWhiteSpace(lines[i]))
			{
				continue;
			}

			var fields = ParseCsvLine(lines[i]);
			Assert.True(fields.Length == header.Length,
				$"entity-features-matrix.csv row {i + 1} has {fields.Length} cells, header has {header.Length} — fix with tools/entity-features.ps1.");
			var entity = fields[entityIdx].Trim();
			Assert.True(entity.Length > 0 && !rows.ContainsKey(entity),
				$"entity-features-matrix.csv row {i + 1} has an empty or duplicate entity '{entity}'.");
			rows.Add(entity, new MatrixRow(entity, fields[syncIdx].Trim().ToLowerInvariant(), fields[pathIdx].Trim()));
		}

		return rows;
	}

	private static List<MarkdownTable> LoadTables()
	{
		Assert.True(File.Exists(DocPath),
			"feature-matrices.md missing from the test output (csproj None copy) — the doc-consistency gate cannot run.");

		var lines = File.ReadAllLines(DocPath);
		var tables = new List<MarkdownTable>();
		var i = 0;
		while (i < lines.Length)
		{
			if (!lines[i].TrimStart().StartsWith("|", StringComparison.Ordinal))
			{
				i++;
				continue;
			}

			var start = i;
			while (i < lines.Length && lines[i].TrimStart().StartsWith("|", StringComparison.Ordinal))
			{
				i++;
			}

			var block = lines.Skip(start).Take(i - start).ToArray();
			if (block.Length < 2 || !IsSeparatorRow(block[1]))
			{
				continue;
			}

			var header = ParseMarkdownRow(block[0]);
			var rows = new List<string[]>();
			for (var row = 2; row < block.Length; row++)
			{
				rows.Add(ParseMarkdownRow(block[row]));
			}

			tables.Add(new MarkdownTable(header, rows));
		}

		return tables;
	}

	[Fact]
	public void NarrativeTables_MatchEveryMatrixEntityExactlyOnce()
	{
		var matrix = LoadMatrix();
		var violations = new List<string>();
		var seen = new Dictionary<string, int>(StringComparer.Ordinal);

		foreach (var table in LoadTables())
		{
			var entityIdx = ColumnIndex(table.Header, "entity");
			if (entityIdx < 0)
			{
				// Not an entity narrative table: the page also carries the item tables and the column
				// keys. A table that DOES carry `entity` must carry the two columns this gate compares.
				continue;
			}

			var syncIdx = ColumnIndex(table.Header, "sync");
			var pathIdx = ColumnIndex(table.Header, "path");
			if (syncIdx < 0 || pathIdx < 0)
			{
				violations.Add($"the narrative table for '{table.Header[entityIdx]}' must carry sync and path columns");
				continue;
			}

			foreach (var row in table.Rows)
			{
				if (row.Length != table.Header.Length)
				{
					violations.Add($"narrative row '{string.Join(" | ", row)}' has {row.Length} cells, header has {table.Header.Length}");
					continue;
				}

				var names = ParseEntityCell(row[entityIdx]);
				if (names.Count == 0)
				{
					violations.Add($"narrative row '{row[entityIdx]}' names no matrix entity");
					continue;
				}

				var docSync = ParseSyncCell(row[syncIdx]);
				if (docSync == null)
				{
					violations.Add($"narrative row '{row[entityIdx]}' has an unparsable sync cell '{row[syncIdx]}' (covered/excluded/missing)");
					continue;
				}

				var docPath = row[pathIdx].Trim();
				if (docPath.Length == 0)
				{
					violations.Add($"narrative row '{row[entityIdx]}' has an empty path cell (name the covering mechanism)");
					continue;
				}

				foreach (var name in names)
				{
					if (!matrix.TryGetValue(name, out var expected))
					{
						violations.Add($"narrative row '{row[entityIdx]}' names '{name}', which is not in entity-features-matrix.csv");
						continue;
					}

					seen[name] = seen.TryGetValue(name, out var count) ? count + 1 : 1;
					if (docSync != expected.Sync)
					{
						violations.Add($"{name}: narrative sync '{docSync}' disagrees with matrix sync '{expected.Sync}'");
					}
				}
			}
		}

		foreach (var entity in matrix.Keys.OrderBy(x => x, StringComparer.Ordinal))
		{
			if (!seen.ContainsKey(entity))
			{
				violations.Add($"{entity}: present in the matrix but missing from the narrative tables");
			}
		}

		foreach (var pair in seen.Where(p => p.Value != 1).OrderBy(p => p.Key, StringComparer.Ordinal))
		{
			violations.Add($"{pair.Key}: appears {pair.Value} times in the narrative tables (exactly once required)");
		}

		Assert.True(violations.Count == 0,
			$"entity-features doc/matrix disagreement ({violations.Count}):\n" + string.Join("\n", violations));
	}

	/// <summary>The index of a named column, case-insensitively: the matrix writes its header in lower case while a human table writes <c>Entity</c>/<c>Sync</c>/<c>Path</c>, and neither capitalisation is part of the contract.</summary>
	private static int ColumnIndex(string[] header, string name) =>
		Array.FindIndex(header, cell => string.Equals(cell.Trim(), name, StringComparison.OrdinalIgnoreCase));

	private static string[] ParseCsvLine(string line)
	{
		// Quote-aware split: the matrix contract forbids commas in cells, but a
		// future row must not silently break the consistency gate.
		var fields = new List<string>();
		var current = new StringBuilder();
		var inQuotes = false;
		foreach (var ch in line)
		{
			if (ch == '"')
			{
				inQuotes = !inQuotes;
			}
			else if (ch == ',' && !inQuotes)
			{
				fields.Add(current.ToString());
				current.Clear();
			}
			else
			{
				current.Append(ch);
			}
		}

		fields.Add(current.ToString());
		return [.. fields];
	}

	private static string[] ParseMarkdownRow(string line)
	{
		var trimmed = line.Trim();
		Assert.True(trimmed.StartsWith("|", StringComparison.Ordinal) && trimmed.EndsWith("|", StringComparison.Ordinal),
			$"malformed markdown table row: '{line}'");
		var body = trimmed.Substring(1, trimmed.Length - 2);
		return Array.ConvertAll(body.Split(['|']), c => c.Trim());
	}

	private static bool IsSeparatorRow(string line)
	{
		var cells = line.Trim().Trim('|').Split(['|']);
		return cells.All(c => c.Trim().Length > 0 && c.Trim().All(ch => ch is '-' or ':' or ' '));
	}

	private static List<string> ParseEntityCell(string cell)
	{
		var clean = cell.Replace("**", string.Empty).Replace("*", string.Empty).Replace("`", string.Empty).Trim();

		// Entity names carry an optional display qualifier, e.g.
		// "LifepodController (heat button)" or "Openable (locks and crates)" — the
		// matrix key(s) are the text before the qualifier. Strip the qualifier
		// BEFORE splitting, so a separator inside the qualifier cannot split
		// one entity name into two fragments.
		var qualifier = Regex.Match(clean, @"^(.*?)\s*\([^)]*\)$");
		var body = qualifier.Success ? qualifier.Groups[1].Value : clean;

		var names = new List<string>();
		// The page groups entities that share a verdict into one row, separated by
		// `/` or by `,`; both are separators, never part of a matrix key.
		foreach (var token in body.Split(['/', ','], StringSplitOptions.RemoveEmptyEntries))
		{
			var name = token.Trim();
			if (name.Length > 0)
			{
				names.Add(name);
			}
		}

		return names;
	}

	private static string? ParseSyncCell(string cell)
	{
		var text = cell.Replace("**", string.Empty).Trim().ToLowerInvariant();
		return text.StartsWith("covered", StringComparison.Ordinal) ? "covered"
			: text.StartsWith("excluded", StringComparison.Ordinal) ? "excluded"
			: text.StartsWith("missing", StringComparison.Ordinal) ? "missing"
			: null;
	}
}
