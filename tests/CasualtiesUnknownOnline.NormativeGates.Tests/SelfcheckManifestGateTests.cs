using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// The self-check MANIFEST is the register its README says it is: every markdown file under
/// <c>docs/evidence/selfchecks/</c> — the manifest itself excepted — carries exactly one row, no file
/// carries two, every row names a file that exists, the intro points at the directory that is really
/// there, and the index keeps a census floor. The register's own contract is gated too: a status from
/// the recorded vocabulary, a note bounded to 1,200 characters, and — the fact that keeps the two halves of
/// a supersession in step — a <c>historical</c> row exactly when the record itself carries the
/// <c>&gt; **HISTORICAL**</c> banner, named along with a document that is there to read instead.
/// Whether a status was judged well stays a review duty; that is what the intro's own wording says.
/// </summary>
public sealed class SelfcheckManifestGateTests
{
	private const string SelfcheckRoot = "docs/evidence/selfchecks";
	private const string ManifestRelativePath = "docs/evidence/selfchecks/MANIFEST.md";

	/// <summary>The measured size when the gate completed the index: a broken parse must fail loudly
	/// instead of reporting a complete index over an empty file set.</summary>
	private const int IndexFloor = 240;

	/// <summary>The note is bounded to the same 1,200-character ceiling the lessons ledger and the sync
	/// coverage matrix carry — one clause wherever a clause carries it — so a row that grows back into a
	/// cycle narrative fails here.</summary>
	private const int NoteCeiling = 1200;

	private static readonly string[] Statuses = ["current", "historical"];

	/// <summary>The marker a superseded record carries; the sidecar of the census gate's reach check.</summary>
	private const string HistoricalBanner = "> **HISTORICAL**";

	/// <summary>The placeholder the register used to answer a reader with; it is retired.</summary>
	private const string RetiredPlaceholder = "candidate current evidence; verify before citing";

	/// <summary>A note that carries no carrier is written as this instead of an empty cell.</summary>
	private const string NoNote = "—";

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

	[Fact]
	public void EveryRow_CarriesARecordedStatusAndABoundedNote()
	{
		var failures = ShapeFailures(File.ReadAllText(ManifestFile()));
		Assert.True(failures.Length == 0, Describe("the register's row shape", failures));
	}

	[Fact]
	public void EveryHistoricalRow_AgreesWithTheRecordItNames()
	{
		var failures = BannerFailures(File.ReadAllText(ManifestFile()), RecordCarriesTheBanner);
		Assert.True(failures.Length == 0, Describe("a supersession, as the row and the record tell it", failures));
	}

	[Fact]
	public void EveryHistoricalRow_NamesWhereToReadInstead()
	{
		var failures = CarrierFailures(
			File.ReadAllText(ManifestFile()),
			carrier => File.Exists(RepositoryPaths.File(carrier)));
		Assert.True(failures.Length == 0, Describe("the document a superseded row sends its reader to", failures));
	}

	// --- the matcher's own contract: each sample feeds synthetic text to the same pure functions the
	// facts use, so a parser that starts counting prose, or a check that stops checking, fails here ---

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

	[Fact]
	public void TheShapeCheck_FlagsAnUnboundedNoteAnUnknownStatusAndAnEmptyNote()
	{
		var overlong = new string('x', NoteCeiling + 1);
		var failures = ShapeFailures(
			$"| ui/chat-selfcheck.md | UI | current | {overlong} |\n"
			+ "| ui/other-selfcheck.md | UI | done | x |\n"
			+ "| ui/third-selfcheck.md | UI | current | |\n"
			+ "| ui/fourth-selfcheck.md | UI | current | candidate current evidence; verify before citing |");
		Assert.Contains(failures, failure => failure.Contains("ui/chat-selfcheck.md", StringComparison.Ordinal) && failure.Contains("ceiling", StringComparison.Ordinal));
		Assert.Contains(failures, failure => failure.Contains("ui/other-selfcheck.md", StringComparison.Ordinal) && failure.Contains("not a status", StringComparison.Ordinal));
		Assert.Contains(failures, failure => failure.Contains("ui/third-selfcheck.md", StringComparison.Ordinal) && failure.Contains(NoNote, StringComparison.Ordinal));
		Assert.Contains(failures, failure => failure.Contains("ui/fourth-selfcheck.md", StringComparison.Ordinal) && failure.Contains("retired", StringComparison.Ordinal));
	}

	[Fact]
	public void TheBannerCheck_FlagsARowAndARecordThatDisagree()
	{
		var manifestText = "| ui/chat-selfcheck.md | UI | historical | `docs/architecture/current.md` |\n"
			+ "| ui/other-selfcheck.md | UI | current | x |";
		var noBanner = Assert.Single(BannerFailures(manifestText, _ => false));
		Assert.Contains("ui/chat-selfcheck.md", noBanner, StringComparison.Ordinal);
		Assert.Contains("carries no", noBanner, StringComparison.Ordinal);

		var bannered = Assert.Single(BannerFailures(manifestText, _ => true));
		Assert.Contains("ui/other-selfcheck.md", bannered, StringComparison.Ordinal);
		Assert.Contains("while the row says", bannered, StringComparison.Ordinal);
	}

	[Fact]
	public void TheCarrierCheck_FlagsASupersededRowThatNamesNothingOrNamesWhatIsGone()
	{
		var manifestText = "| ui/chat-selfcheck.md | UI | historical | the wire is gone |\n"
			+ "| ui/other-selfcheck.md | UI | historical | replaced by `docs/architecture/current.md` |\n"
			+ "| ui/third-selfcheck.md | UI | current | nothing to check here |";
		var missing = Assert.Single(CarrierFailures(manifestText, carrier => carrier.EndsWith("current.md", StringComparison.Ordinal)));
		Assert.Contains("ui/chat-selfcheck.md", missing, StringComparison.Ordinal);
		Assert.Contains("names no document", missing, StringComparison.Ordinal);

		var gone = CarrierFailures(manifestText, _ => false);
		Assert.Equal(2, gone.Length);
		Assert.Contains(gone, failure => failure.Contains("ui/other-selfcheck.md", StringComparison.Ordinal) && failure.Contains("not there", StringComparison.Ordinal));
		Assert.DoesNotContain(gone, failure => failure.Contains("ui/third-selfcheck.md", StringComparison.Ordinal));
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

	/// <summary>The four cells of every table row in the manifest. The header row, the separator and
	/// any prose line that merely names a file are not rows.</summary>
	private static Row[] Rows(string manifestText) =>
		[.. manifestText.Split('\n')
			.Select(line => line.Trim())
			.Where(line => line.StartsWith('|') && line.EndsWith('|'))
			.Select(line => line.Split('|'))
			.Where(cells => cells.Length >= 6)
			.Select(cells => new Row(
				cells[1].Trim().Trim('`'),
				cells[2].Trim(),
				cells[3].Trim(),
				string.Join("|", cells[4..^1]).Trim()))
			.Where(row => row.File.EndsWith(".md", StringComparison.Ordinal))];

	private static string[] IndexedFiles(string manifestText) => [.. Rows(manifestText).Select(row => row.File)];

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

	/// <summary>A row is a status from the recorded vocabulary and a note bounded to one clause: the
	/// placeholder the register used to answer with, an unknown status and an empty note all fail.</summary>
	private static string[] ShapeFailures(string manifestText) =>
		[.. Rows(manifestText).SelectMany(row => RowShapeFailures(row))];

	private static IEnumerable<string> RowShapeFailures(Row row)
	{
		if (!Statuses.Contains(row.Status, StringComparer.Ordinal))
		{
			yield return $"{row.File}: `{row.Status}` is not a status ({string.Join(", ", Statuses)})";
		}

		if (row.Note.Length > NoteCeiling)
		{
			yield return $"{row.File}: its note is {row.Note.Length} characters, over the {NoteCeiling}-character ceiling";
		}

		if (row.Note.Length == 0)
		{
			yield return $"{row.File}: its note is empty — write `{NoNote}` when the sheet's own text is the whole record of it";
		}

		if (row.Note.Contains(RetiredPlaceholder, StringComparison.Ordinal))
		{
			yield return $"{row.File}: its note is the retired placeholder, which answered a reader with nothing";
		}
	}

	/// <summary>A supersession lives in two places that must agree: the row says <c>historical</c> exactly
	/// when the record carries the banner, so neither half can rot away from the other. A record that is
	/// not there is answered by <c>null</c> and left to the resolvability fact.</summary>
	private static string[] BannerFailures(string manifestText, Func<string, bool?> recordCarriesTheBanner)
	{
		var failures = new List<string>();
		foreach (var row in Rows(manifestText))
		{
			if (recordCarriesTheBanner(row.File) is not { } bannered)
			{
				continue;
			}

			if (row.Status == "historical" && !bannered)
			{
				failures.Add($"{row.File}: the row says `historical` while the record carries no {HistoricalBanner} banner");
			}

			if (row.Status != "historical" && bannered)
			{
				failures.Add($"{row.File}: the record carries a {HistoricalBanner} banner while the row says `{row.Status}`");
			}
		}

		return [.. failures];
	}

	/// <summary>A superseded row sends its reader somewhere: the note names at least one repository-relative
	/// <c>docs/</c> document, and each one it names is there.</summary>
	private static string[] CarrierFailures(string manifestText, Func<string, bool> carrierExists)
	{
		var failures = new List<string>();
		foreach (var row in Rows(manifestText).Where(row => row.Status == "historical"))
		{
			var carriers = Carriers(row.Note);
			if (carriers.Length == 0)
			{
				failures.Add($"{row.File}: a `historical` row names no document to read instead");
				continue;
			}

			failures.AddRange(carriers
				.Where(carrier => !carrierExists(carrier))
				.Select(carrier => $"{row.File}: it names {carrier}, which is not there"));
		}

		return [.. failures];
	}

	/// <summary>The repository-relative <c>docs/</c> paths a note names, read out of its backticked spans.</summary>
	private static string[] Carriers(string note) =>
		[.. note.Split('`')
			.Where((span, index) => index % 2 == 1 && span.StartsWith("docs/", StringComparison.Ordinal))
			.Distinct(StringComparer.Ordinal)];

	/// <summary>Whether the record carries the banner, or <c>null</c> when there is no record to read —
	/// a row naming a file that is not there is the resolvability fact's finding, not this one's, so this
	/// fact stays silent rather than reporting a missing file as an unbannered one.</summary>
	private static bool? RecordCarriesTheBanner(string file)
	{
		var path = RepositoryPaths.File($"{SelfcheckRoot}/{file}");
		return File.Exists(path)
			? File.ReadAllText(path).Contains(HistoricalBanner, StringComparison.Ordinal)
			: null;
	}

	private static string Describe(string check, string[] failures) =>
		$"{check}: {failures.Length} violation(s){Environment.NewLine}{string.Join(Environment.NewLine, failures)}";

	private static void AssertSame(string[] expected, string[] actual, string because) =>
		Assert.True(
			expected.SequenceEqual(actual, StringComparer.Ordinal),
			$"{because}: expected [{string.Join(", ", expected)}], got [{string.Join(", ", actual)}]");

	private sealed record Row(string File, string Domain, string Status, string Note);
}
