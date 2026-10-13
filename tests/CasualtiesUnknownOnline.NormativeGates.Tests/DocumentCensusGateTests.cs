using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// The document census — <c>docs/standard/document-census.txt</c> — carries exactly one row per markdown
/// document git tracks, and every row names the document that reaches it: a citation for a record, a
/// section index for a human page, the loader for an instruction file. A row that can name nothing is the
/// defect the audit exists to find (<c>docs/backlog/review/document-corpus-audit.md</c>), so a new document
/// cannot land without being referenced by one that already exists. The rule is <c>docs/AGENTS.md</c> §6.
/// A green run proves every document has an access path; it cannot judge whether a page teaches.
/// </summary>
public sealed class DocumentCensusGateTests
{
	private const string CensusPath = "docs/standard/document-census.txt";

	/// <summary>The measured row count when the census was laid down (966): a broken parse must fail loudly
	/// rather than report a complete census over an empty row set.</summary>
	private const int CensusFloor = 900;

	/// <summary>
	/// The size budget for one document: measured to sit in the gap between the four append-only logs the
	/// audit queued for rework (111,705 to 268,673 bytes) and the largest document that is not a log
	/// (107,575 bytes). A document over it may stand only while its census row says <c>rework</c> — the gate
	/// cannot tell a log from a long page, so it makes the row carry that judgement instead of guessing.
	/// </summary>
	private const int DocumentBudgetBytes = 114688;

	/// <summary>Build output, the vendored decompiled tree and the gitignored local areas carry no documents.</summary>
	private static readonly string[] SkippedDirectories =
		["bin", "obj", ".git", ".vs", "reversing", "node_modules", "artifacts", ".agent-local", ".acceptance"];

	private static readonly string[] Dispositions = ["keep", "rework", "delete"];
	private static readonly string[] Readers = ["loaded", "index", "spec", "claim", "attribution", "none"];

	/// <summary>A file a reader opens directly, with no other document pointing at it: a loader file — the
	/// root instructions and their second entry point — or an area index.</summary>
	private static readonly string[] EntryPointNames = ["README.md", "README.zh.md", "AGENTS.md", "index.md", "CLAUDE.md"];

	[Fact]
	public void EveryTrackedDocument_HasExactlyOneRow()
	{
		var documents = Documents();
		var rows = Rows(CensusText());
		Assert.True(
			documents.Count >= CensusFloor,
			$"discovery found {documents.Count} document(s), below the census floor {CensusFloor} — the scan surface moved");
		Assert.True(
			rows.Count >= CensusFloor,
			$"the census carries {rows.Count} row(s), below the census floor {CensusFloor} — the parser or the census broke");

		var failures = CompletenessFailures(documents, rows);
		Assert.True(failures.Count == 0, Describe("census completeness", failures));
	}

	[Fact]
	public void EveryRow_NamesADocumentThatExists()
	{
		var dangling = Rows(CensusText())
			.Where(row => !File.Exists(RepositoryPaths.File(row.Path)))
			.Select(row => $"{row.Path}: the census names a document that is not there")
			.ToList();
		Assert.True(dangling.Count == 0, Describe("census rows", dangling));
	}

	[Fact]
	public void EveryRow_CarriesFourCellsAndARecordedVocabulary()
	{
		var failures = ShapeFailures(CensusText()).Concat(VocabularyFailures(Rows(CensusText()))).ToList();
		Assert.True(failures.Count == 0, Describe("census row shape", failures));
	}

	[Fact]
	public void EveryKeepRow_IsNamedBackByTheDocumentItNames()
	{
		var failures = new List<string>();
		foreach (var row in Rows(CensusText()).Where(row => row.Disposition == "keep"))
		{
			if (row.Reach == "-")
			{
				if (!EntryPointNames.Contains(FileName(row.Path), StringComparer.Ordinal))
				{
					failures.Add($"{row.Path}: no document reaches it, and it is not an entry point — name the document that does, or queue it for rework");
				}

				continue;
			}

			var reach = RepositoryPaths.File(row.Reach);
			if (!File.Exists(reach))
			{
				failures.Add($"{row.Path}: its row names {row.Reach}, which is not there");
				continue;
			}

			if (!NamesTheDocument(File.ReadAllText(reach), row.Path))
			{
				failures.Add($"{row.Path}: its row names {row.Reach}, but that document never names it");
			}
		}

		Assert.True(failures.Count == 0, Describe("the reach of a kept document", failures));
	}

	[Fact]
	public void EveryReworkOrDeleteRow_NamesTheTicketThatCarriesIt()
	{
		var failures = new List<string>();
		foreach (var row in Rows(CensusText()).Where(row => row.Disposition is "rework" or "delete"))
		{
			if (!row.Reach.StartsWith("docs/backlog/", StringComparison.Ordinal) || !File.Exists(RepositoryPaths.File(row.Reach)))
			{
				failures.Add($"{row.Path}: `{row.Disposition}` must name the backlog ticket that carries the work, not `{row.Reach}`");
			}
		}

		Assert.True(failures.Count == 0, Describe("the work queue behind rework and delete", failures));
	}

	[Fact]
	public void EveryDocument_OverTheBudgetIsOnTheReworkList()
	{
		var sizes = Rows(CensusText())
			.Where(row => File.Exists(RepositoryPaths.File(row.Path)))
			.Select(row => (row.Path, new FileInfo(RepositoryPaths.File(row.Path)).Length, row.Disposition));
		var failures = BudgetFailures(sizes, DocumentBudgetBytes);
		Assert.True(failures.Count == 0, Describe("the document size budget", failures));
	}

	// --- the matcher's own contract: each sample feeds synthetic text to the same pure functions the facts
	// use, so a parser that starts counting prose, or a reach check that stops checking, fails here ---

	[Fact]
	public void TheRowParser_ReadsOnlyTheRowShape()
	{
		var text = """
			# A comment naming docs/backlog/done/x.md.
			Prose naming docs/backlog/done/y.md.

			docs/backlog/done/x.md | keep | index | docs/backlog/README.md
			docs/backlog/done/z.md | keep | index
			""";
		var row = Assert.Single(Rows(text));
		Assert.Equal("docs/backlog/done/x.md", row.Path);
		Assert.Contains(ShapeFailures(text), failure => failure.Contains("docs/backlog/done/z.md", StringComparison.Ordinal));
	}

	[Fact]
	public void TheCompletenessCheck_FlagsADocumentWithoutARow()
	{
		var rows = Rows("docs/backlog/done/x.md | keep | index | docs/backlog/README.md");
		var failures = CompletenessFailures(["docs/backlog/done/x.md", "docs/backlog/done/y.md"], rows);
		var failure = Assert.Single(failures);
		Assert.Contains("docs/backlog/done/y.md", failure, StringComparison.Ordinal);
		Assert.Empty(CompletenessFailures(["docs/backlog/done/x.md"], rows));
	}

	[Fact]
	public void TheReachCheck_FlagsAnUnreachedDocument()
	{
		Assert.True(NamesTheDocument("see docs/evidence/acceptance/record.md for the run", "docs/evidence/acceptance/record.md"));
		Assert.True(NamesTheDocument("the record is `record.md`", "docs/evidence/acceptance/record.md"));
		Assert.False(NamesTheDocument("a document about something else", "docs/evidence/acceptance/record.md"));
	}

	[Fact]
	public void TheReachCheck_FlagsAPageItsIndexDoesNotList()
	{
		// A human page is reached from its section index; a bare `README.md` elsewhere is not that link, so
		// the page name and its directory have to appear together.
		Assert.True(NamesTheDocument("- [Start here](start/README.md)", "docs/en/start/README.md"));
		Assert.False(NamesTheDocument("- [Documentation](../README.md)", "docs/en/start/README.md"));
	}

	[Fact]
	public void TheBudgetCheck_FlagsAnOversizedDocumentThatIsNotOnTheReworkList()
	{
		var failures = BudgetFailures(
			[("docs/decisions/active.md", DocumentBudgetBytes + 1, "keep"), ("docs/decisions/archive.md", DocumentBudgetBytes, "keep")],
			DocumentBudgetBytes);
		var failure = Assert.Single(failures);
		Assert.Contains("docs/decisions/active.md", failure, StringComparison.Ordinal);
	}

	/// <summary>Every markdown document git tracks, repository-relative. The scan walks the working tree —
	/// the acceptance re-derives it against <c>git ls-files</c> — so the gitignored local areas are skipped
	/// by name, and the one tracked file inside the vendored tree is added back. Two shapes therefore stay
	/// outside what this gate can see: a tracked document inside a directory the walk skips, and an untracked
	/// document in the tree — which the completeness check would instead demand a row for. Both are census duties.</summary>
	private static IReadOnlyList<string> Documents()
	{
		var found = new List<string>();
		var pending = new Stack<string>();
		pending.Push(RepositoryPaths.Root);
		while (pending.Count > 0)
		{
			var directory = pending.Pop();
			foreach (var child in Directory.EnumerateDirectories(directory))
			{
				if (!SkippedDirectories.Contains(Path.GetFileName(child), StringComparer.OrdinalIgnoreCase))
				{
					pending.Push(child);
				}
			}

			foreach (var file in Directory.EnumerateFiles(directory, "*.md"))
			{
				var name = Path.GetFileName(file);
				if (name.EndsWith(".local.md", StringComparison.OrdinalIgnoreCase) || name.StartsWith("AGENTS.local", StringComparison.Ordinal))
				{
					continue;
				}

				found.Add(Relative(file));
			}
		}

		found.Add("reversing/README.md");
		return [.. found.OrderBy(relative => relative, StringComparer.Ordinal)];
	}

	private static string Relative(string absolute) =>
		absolute.Substring(RepositoryPaths.Root.Length).TrimStart('\\', '/').Replace('\\', '/');

	private static string CensusText() => File.ReadAllText(RepositoryPaths.File(CensusPath));

	/// <summary>The well-formed rows: `&lt;path&gt; | &lt;disposition&gt; | &lt;reader&gt; | &lt;reach&gt;`. Comments and blank
	/// lines are not rows; a malformed line is reported by the shape check, not parsed away in silence.</summary>
	private static IReadOnlyList<Row> Rows(string text) =>
		[.. RowsAndMalformed(text).Rows];

	private static IReadOnlyList<string> ShapeFailures(string text) =>
		[.. RowsAndMalformed(text).Malformed];

	private static (IReadOnlyList<Row> Rows, IReadOnlyList<string> Malformed) RowsAndMalformed(string text)
	{
		var rows = new List<Row>();
		var malformed = new List<string>();
		foreach (var line in text.Split('\n'))
		{
			var trimmed = line.Trim();
			if (trimmed.Length == 0 || trimmed.StartsWith('#'))
			{
				continue;
			}

			var cells = trimmed.Split('|').Select(cell => cell.Trim()).ToArray();
			if (cells.Length != 4 || cells.Any(cell => cell.Length == 0))
			{
				malformed.Add($"{trimmed}: not `<path> | <disposition> | <reader> | <reach>`");
				continue;
			}

			rows.Add(new Row(cells[0], cells[1], cells[2], cells[3]));
		}

		return (rows, malformed);
	}

	private static IReadOnlyList<string> VocabularyFailures(IReadOnlyList<Row> rows)
	{
		var failures = new List<string>();
		foreach (var row in rows)
		{
			if (!Dispositions.Contains(row.Disposition, StringComparer.Ordinal))
			{
				failures.Add($"{row.Path}: `{row.Disposition}` is not a disposition ({string.Join(", ", Dispositions)})");
			}

			if (!Readers.Contains(row.Reader, StringComparer.Ordinal))
			{
				failures.Add($"{row.Path}: `{row.Reader}` is not a reader ({string.Join(", ", Readers)})");
			}

			if (row.Disposition == "keep" && row.Reader == "none")
			{
				failures.Add($"{row.Path}: a kept document has a reader; `none` belongs to rework and delete rows");
			}

			if (row.Disposition is "rework" or "delete" && row.Reader != "none")
			{
				failures.Add($"{row.Path}: a `{row.Disposition}` row names no reader — it names the work queue in `reach`");
			}
		}

		return failures;
	}

	private static IReadOnlyList<string> CompletenessFailures(IReadOnlyList<string> documents, IReadOnlyList<Row> rows)
	{
		var paths = new HashSet<string>(rows.Select(row => row.Path), StringComparer.Ordinal);
		var failures = documents.Where(document => !paths.Contains(document)).Select(document => $"{document}: no census row").ToList();
		failures.AddRange(rows
			.GroupBy(row => row.Path, StringComparer.Ordinal)
			.Where(group => group.Count() > 1)
			.Select(group => $"{group.Key}: {group.Count()} census rows"));
		return failures;
	}

	/// <summary>The budget is on the document, not on the row: a document over it may stand only while its
	/// row says <c>rework</c>, which is where the audit's queue is.</summary>
	internal static IReadOnlyList<string> BudgetFailures(IEnumerable<(string Path, long Size, string Disposition)> documents, int budget) =>
		[.. documents
			.Where(document => document.Size > budget && document.Disposition != "rework")
			.OrderBy(document => document.Path, StringComparer.Ordinal)
			.Select(document => $"{document.Path}: {document.Size} bytes, over the {budget}-byte budget, but its census row says `{document.Disposition}` — queue it for rework or split it")];

	/// <summary>True when <paramref name="text"/> names <paramref name="target"/>. A citation may drop the
	/// extension and a batch scope record is named by its batch id; an index page is named with the
	/// directory it indexes, because a bare `README.md` says nothing about which README it means.</summary>
	internal static bool NamesTheDocument(string text, string target)
	{
		var name = FileName(target);
		var parent = ParentName(target);
		if (EntryPointNames.Contains(name, StringComparer.Ordinal))
		{
			return parent.Length > 0 && text.Contains($"{parent}/{name}", StringComparison.Ordinal);
		}

		if (target.EndsWith("-scope.md", StringComparison.Ordinal))
		{
			return text.Contains(name, StringComparison.Ordinal) || text.Contains(name[..^"-scope.md".Length], StringComparison.Ordinal);
		}

		return text.Contains(name, StringComparison.Ordinal) || text.Contains(name[..^".md".Length], StringComparison.Ordinal);
	}

	private static string FileName(string path) => path[(path.LastIndexOf('/') + 1)..];

	private static string ParentName(string path)
	{
		var head = path[..path.LastIndexOf('/')];
		return head[(head.LastIndexOf('/') + 1)..];
	}

	private static string Describe(string check, IReadOnlyList<string> failures) =>
		$"{check}: {failures.Count} violation(s){Environment.NewLine}{string.Join(Environment.NewLine, failures)}";

	private sealed record Row(string Path, string Disposition, string Reader, string Reach);
}
