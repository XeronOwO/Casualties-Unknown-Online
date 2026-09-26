using System;
using System.IO;
using System.Linq;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// The self-check MANIFEST is the complete per-file index its README says it is: every markdown file
/// under <c>docs/evidence/selfchecks/</c> — the manifest itself excepted — carries exactly one row,
/// every row
/// names a file that exists, and the intro points at the directory that is really there. The gate
/// proves completeness and resolvability — whether a row's status was judged well stays a review
/// duty, which is what the manifest's own "candidate current evidence; verify before citing" says.
/// </summary>
public sealed class SelfcheckManifestGateTests
{
	private const string SelfcheckRoot = "docs/evidence/selfchecks";
	private const string ManifestRelativePath = "docs/evidence/selfchecks/MANIFEST.md";

	/// <summary>The measured size when the gate completed the index: a broken parse must fail loudly
	/// instead of reporting a complete index over an empty file set.</summary>
	private const int IndexFloor = 240;

	[Fact]
	public void EverySelfcheckFile_HasExactlyOneManifestRow()
	{
		var manifestText = File.ReadAllText(ManifestFile());
		var missing = FilesWithoutRows(manifestText, SelfcheckFiles());
		var duplicated = FilesWithMoreThanOneRow(manifestText);
		Assert.True(
			missing.Length == 0,
			$"{missing.Length} self-check file(s) have no MANIFEST row: {string.Join(", ", missing)}");
		Assert.True(
			duplicated.Length == 0,
			$"{duplicated.Length} self-check file(s) carry more than one MANIFEST row: {string.Join(", ", duplicated)}");
	}

	[Fact]
	public void EveryRow_NamesASelfcheckFileThatExists()
	{
		var dangling = RowsWithoutFiles(File.ReadAllText(ManifestFile()), SelfcheckFiles());
		Assert.True(
			dangling.Length == 0,
			$"{dangling.Length} MANIFEST row(s) name a file that is not there: {string.Join(", ", dangling)}");
	}

	[Fact]
	public void TheManifest_NamesTheDirectoryItIndexes()
	{
		var manifestText = File.ReadAllText(ManifestFile());
		Assert.True(
			manifestText.Contains("docs/evidence/selfchecks/", StringComparison.Ordinal),
			"the MANIFEST no longer names the tree it indexes (docs/evidence/selfchecks/)");
		Assert.False(
			manifestText.Contains("docs/selfchecks/", StringComparison.Ordinal),
			"the MANIFEST still points at the retired docs/selfchecks/ path");
	}

	[Fact]
	public void TheIndex_MeetsItsFloor()
	{
		var files = SelfcheckFiles();
		var rows = IndexedFiles(File.ReadAllText(ManifestFile()));
		Assert.True(
			files.Length >= IndexFloor,
			$"the self-check tree holds {files.Length} file(s), below the census floor {IndexFloor}: the scan surface moved");
		Assert.True(
			rows.Length >= IndexFloor,
			$"the MANIFEST carries {rows.Length} row(s), below the census floor {IndexFloor}: the parser or the index broke");
	}

	// --- the matcher's own contract: each sample feeds synthetic text to the same pure functions the
	// facts use, so a parser that starts counting prose, or stops seeing a row, fails here ---

	[Fact]
	public void TheIndex_ReadsOnlyTheRowShape()
	{
		var manifestText = """
			Some prose naming `players/carry-interaction-selfcheck.md`.
			- a bullet naming `players/character-sound-selfcheck.md`
			| File | Domain | Status | Note |
			|---|---|---|---|
			| ui/chat-selfcheck.md | UI | current | see players/character-sound-selfcheck.md |
			""";
		AssertSame(["ui/chat-selfcheck.md"], IndexedFiles(manifestText), "a prose line or a note is not an index row");
	}

	[Fact]
	public void TheIndex_ReportsAFileWithoutARow()
	{
		var manifestText = "| ui/chat-selfcheck.md | UI | current | x |";
		AssertSame(
			["ui/missing-selfcheck.md"],
			FilesWithoutRows(manifestText, ["ui/chat-selfcheck.md", "ui/missing-selfcheck.md"]),
			"a file the index does not list is reported");
	}

	[Fact]
	public void TheIndex_ReportsARowWithoutAFile()
	{
		var manifestText = "| ui/chat-selfcheck.md | UI | current | x |\n| ui/gone-selfcheck.md | UI | current | x |";
		AssertSame(
			["ui/gone-selfcheck.md"],
			RowsWithoutFiles(manifestText, ["ui/chat-selfcheck.md"]),
			"a row whose file is gone is reported");
	}

	private static string ManifestFile() => Path.Combine(RepositoryPaths.Root, ManifestRelativePath);

	private static string[] SelfcheckFiles()
	{
		var root = Path.Combine(RepositoryPaths.Root, SelfcheckRoot);
		return [.. Directory.GetFiles(root, "*.md", SearchOption.AllDirectories)
			.Where(path => !path.EndsWith(ManifestRelativePath.Substring(SelfcheckRoot.Length + 1), StringComparison.Ordinal))
			.Select(path => path.Substring(root.Length + 1).Replace('\\', '/'))
			.OrderBy(file => file, StringComparer.Ordinal)];
	}

	/// <summary>The first cell of every table row in the manifest. The header row, the separator and
	/// any prose line that merely names a file are not rows.</summary>
	private static string[] IndexedFiles(string manifestText) =>
		[.. manifestText.Split('\n')
			.Select(line => line.Trim())
			.Where(line => line.StartsWith("|", StringComparison.Ordinal) && line.EndsWith("|", StringComparison.Ordinal))
			.Select(line => line.Split('|'))
			.Where(cells => cells.Length >= 6)
			.Select(cells => cells[1].Trim().Trim('`'))
			.Where(name => name.EndsWith(".md", StringComparison.Ordinal))];

	private static string[] FilesWithoutRows(string manifestText, string[] files)
	{
		var rows = IndexedFiles(manifestText);
		return [.. files.Where(file => !rows.Contains(file, StringComparer.Ordinal)).OrderBy(file => file, StringComparer.Ordinal)];
	}

	private static string[] RowsWithoutFiles(string manifestText, string[] files) =>
		[.. IndexedFiles(manifestText)
			.Where(row => !files.Contains(row, StringComparer.Ordinal))
			.Distinct(StringComparer.Ordinal)
			.OrderBy(row => row, StringComparer.Ordinal)];

	private static string[] FilesWithMoreThanOneRow(string manifestText) =>
		[.. IndexedFiles(manifestText)
			.GroupBy(row => row, StringComparer.Ordinal)
			.Where(group => group.Count() > 1)
			.Select(group => group.Key)
			.OrderBy(row => row, StringComparer.Ordinal)];

	private static void AssertSame(string[] expected, string[] actual, string because) =>
		Assert.True(
			expected.SequenceEqual(actual, StringComparer.Ordinal),
			$"{because}: expected [{string.Join(", ", expected)}], got [{string.Join(", ", actual)}]");
}
