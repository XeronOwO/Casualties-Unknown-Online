using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// The two feature matrices live in `docs/contracts/` and are read through `tools/item-features.ps1`
/// and `tools/entity-features.ps1`, each resolving its own CSV through a `Join-Path $scriptDir` path
/// literal. Nothing read those literals: the documentation and backlog link gates walk markdown links,
/// not PowerShell strings, so a tool could stop resolving its own matrix — renamed, moved to another
/// root variable, or pointed at a path that never existed — while the rest of the gate set stayed
/// green. This gate derives its scan surface from `tools/*.ps1`, asserts that every script-relative
/// literal names a file that exists and that each matrix tool still resolves its own matrix, and keeps
/// a discovery floor over the scripts it walked, so a scan that stops seeing the directory fails here.
/// The feature columns of both matrices are additionally compared with the column lists the two
/// reference pages publish, in order, because no test read the item CSV before and the entity page's
/// stated column count had drifted from its own table. Both matchers carry synthetic samples, including
/// the shapes they must not read.
/// </summary>
public class FeatureMatrixToolPathGateTests
{
	private const string ItemMatrixPath = "docs/contracts/item-features-matrix.csv";
	private const string EntityMatrixPath = "docs/contracts/entity-features-matrix.csv";

	/// <summary>Each matrix the contract index publishes, with the tool that must keep resolving it.</summary>
	private static readonly (string Script, string Matrix)[] MatrixTools =
	[
		("tools/item-features.ps1", ItemMatrixPath),
		("tools/entity-features.ps1", EntityMatrixPath)
	];

	/// <summary>Six scripts live under tools/ today; the floor keeps a renamed or excluded directory visible.</summary>
	private const int MinimumToolScripts = 4;

	/// <summary>The feature-column counts the reference pages state in prose and the CSVs must carry.</summary>
	private const int ItemFeatureColumnCount = 12;
	private const int EntityFeatureColumnCount = 9;

	private static readonly Regex ScriptRelativeJoinPath = new(
		@"Join-Path\s+\$scriptDir\s+(?<quote>['""])(?<path>[^'""]+)\k<quote>",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);

	private static readonly Regex PageColumnRow = new(
		@"^\|\s*`(?<column>[a-z][a-z0-9-]*)`\s*\|",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);

	[Fact]
	public void EveryScriptRelativeLiteral_NamesAFileThatExistsAndResolvesItsOwnMatrix()
	{
		var scripts = ToolScripts();
		Assert.True(
			scripts.Count >= MinimumToolScripts,
			$"the tools walk found {scripts.Count} script(s) under tools/; the floor is {MinimumToolScripts} — the discovery filter stopped seeing the directory");

		var literals = scripts
			.SelectMany(script => LiteralsIn(script).Select(literal => (Script: script, Literal: literal)))
			.ToList();

		var failures = literals
			.Where(entry => !File.Exists(Resolve(entry.Script, entry.Literal)))
			.Select(entry => $"{entry.Script} → {entry.Literal}")
			.ToList();
		Assert.True(
			failures.Count == 0,
			$"{failures.Count} script-relative path literal(s) name no file: {string.Join("; ", failures)}");

		// Naming *a* file is not enough: the literal has to be the tool's own matrix. A renamed tool, a
		// literal moved to another root variable or a matrix moved out of docs/contracts/ leaves the scan
		// without dropping a count, so the pairing is asserted per tool.
		var resolved = literals
			.Select(entry => (entry.Script, Matrix: RelativePath(Resolve(entry.Script, entry.Literal))))
			.ToList();
		var uncovered = MatrixTools
			.Where(tool => !resolved.Contains((tool.Script, tool.Matrix)))
			.Select(tool => $"{tool.Script} → {tool.Matrix}")
			.ToList();
		Assert.True(
			uncovered.Count == 0,
			$"no script-relative literal resolves {string.Join(", ", uncovered)} any more — the script moved, the literal changed shape, or the matrix left docs/contracts/");
	}

	[Fact]
	public void TheLiteralMatcher_ReadsTheJoinPathFormAndIgnoresTheOtherForms()
	{
		const string text = """
			$matrixPath = Join-Path $scriptDir '..\docs\contracts\item-features-matrix.csv'
			$other = Join-Path $root '..\docs\contracts\entity-features-matrix.csv'
			$quoted = Join-Path $scriptDir "..\docs\contracts\entity-features-matrix.csv"
			$bare = Join-Path $scriptDir $relative
			$escaped = Join-Path $scriptDir 'it''em.csv'
			""";

		var literals = Matches(text);

		Assert.True(literals.Count == 3, $"expected the two $scriptDir literals plus the escaped one, found {literals.Count}: {string.Join("; ", literals)}");
		Assert.Contains(@"..\docs\contracts\item-features-matrix.csv", literals);
		Assert.Contains(@"..\docs\contracts\entity-features-matrix.csv", literals);
		// A doubled quote ends the literal early, so a path holding an apostrophe reads as its prefix: the
		// tools carry no such path, and one would be reported as a missing file rather than missed.
		Assert.Contains("it", literals);
	}

	[Fact]
	public void TheMatrixHeaders_EqualTheFeatureColumnListsTheReferencePagesPublish()
	{
		AssertMatrixColumns(ItemMatrixPath, "item", ItemFeatureColumnCount, "## Item matrix: the feature columns", "## 物品矩阵的列");
		AssertMatrixColumns(EntityMatrixPath, "entity", EntityFeatureColumnCount, "## Entity matrix: the columns", "## 实体矩阵的列");
	}

	[Fact]
	public void ThePageColumnMatcher_ReadsOnlyTheBacktickedFirstColumn()
	{
		string[] lines =
		[
			"## Columns",
			string.Empty,
			"| Column | What it covers |",
			"|---|---|",
			"| `battery` | battery compartment and charge drains |",
			"| `one-shot` | the entity consumes itself |",
			"| plain | not a feature column |",
			"| `two.words` | also not one |",
			"### A subsection",
			"| `nested` | past a sub-heading, which ends the scan |",
			"## Next",
			"| `outside` | past the section |"
		];

		var columns = FeatureColumnsIn(lines, "## Columns");

		Assert.True(columns.Count == 2, $"expected the two backticked columns, found {columns.Count}: {string.Join(", ", columns)}");
		Assert.Contains("battery", columns);
		Assert.Contains("one-shot", columns);
	}

	private static void AssertMatrixColumns(
		string matrixPath,
		string firstColumn,
		int featureColumnCount,
		string englishHeading,
		string chineseHeading)
	{
		var lines = File.ReadAllLines(RepositoryPaths.File(matrixPath));
		Assert.True(lines.Length >= 2, $"{matrixPath} needs a header and at least one data row.");
		var header = lines[0].Split(',').Select(cell => cell.Trim().Trim('"')).ToList();
		Assert.True(
			header.Count >= 2 && string.Equals(header[0], firstColumn, StringComparison.Ordinal),
			$"{matrixPath} must keep '{firstColumn}' as its first column, found '{header[0]}'.");

		var matrixColumns = header.Skip(1).ToList();
		Assert.True(
			matrixColumns.Count == featureColumnCount,
			$"{matrixPath} carries {matrixColumns.Count} feature columns; the reference pages state {featureColumnCount}.");

		foreach (var (page, heading) in new[]
		{
			("docs/en/reference/feature-matrices.md", englishHeading),
			("docs/zh/reference/feature-matrices.md", chineseHeading)
		})
		{
			var pageColumns = FeatureColumns(page, heading);
			Assert.True(
				matrixColumns.SequenceEqual(pageColumns, StringComparer.Ordinal),
				$"{matrixPath} lists {string.Join(", ", matrixColumns)}; {page} lists {string.Join(", ", pageColumns)}");
		}
	}

	private static List<string> ToolScripts() =>
		[.. Directory.EnumerateFiles(RepositoryPaths.File("tools"), "*.ps1", SearchOption.TopDirectoryOnly)
			.Select(RelativePath)
			.OrderBy(relative => relative, StringComparer.Ordinal)];

	private static List<string> LiteralsIn(string scriptRelative) =>
		Matches(File.ReadAllText(RepositoryPaths.File(scriptRelative)));

	private static List<string> Matches(string text) =>
		[.. ScriptRelativeJoinPath.Matches(text)
			.Select(match => match.Groups["path"].Value)];

	private static string Resolve(string scriptRelative, string literal)
	{
		var relative = literal
			.Replace('\\', Path.DirectorySeparatorChar)
			.Replace('/', Path.DirectorySeparatorChar);
		var scriptDirectory = Path.GetDirectoryName(RepositoryPaths.File(scriptRelative)) ?? RepositoryPaths.Root;
		return Path.GetFullPath(Path.Combine(scriptDirectory, relative));
	}

	private static string RelativePath(string absolute) =>
		Path.GetRelativePath(RepositoryPaths.Root, absolute).Replace(Path.DirectorySeparatorChar, '/');

	private static List<string> FeatureColumns(string relative, string heading) =>
		FeatureColumnsIn(File.ReadAllLines(RepositoryPaths.File(relative)), heading);

	private static List<string> FeatureColumnsIn(IReadOnlyList<string> lines, string heading)
	{
		var start = -1;
		for (var i = 0; i < lines.Count; i++)
		{
			if (string.Equals(lines[i].Trim(), heading, StringComparison.Ordinal))
			{
				start = i;
				break;
			}
		}

		Assert.True(start >= 0, $"the page no longer carries its '{heading}' section — update this gate together with the page");

		var columns = new List<string>();
		for (var i = start + 1; i < lines.Count && !lines[i].StartsWith("##", StringComparison.Ordinal); i++)
		{
			var match = PageColumnRow.Match(lines[i].Trim());
			if (match.Success)
			{
				columns.Add(match.Groups["column"].Value);
			}
		}

		Assert.True(columns.Count > 0, $"the page carries no column table under '{heading}'");
		return columns;
	}
}
