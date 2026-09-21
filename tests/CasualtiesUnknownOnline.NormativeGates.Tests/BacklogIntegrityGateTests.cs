using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// Backlog integrity rot guard. The backlog is the project's work queue, so its index and its tickets
/// must not drift apart: every ticket under a status folder is linked from
/// <c>docs/backlog/README.md</c> exactly once AND in the section that matches its folder, every index
/// link resolves, every index ROW stays a POINTER — within its length budget and carrying the
/// priority its ticket declares, because an index that re-states its tickets is a second copy that
/// drifts — every ticket's <c>- Status:</c> field agrees with the folder that holds it (the
/// folder is the status; the field repeats it for a reader who opens the file), every test anchor a
/// LIVE ticket cites (<c>SomeTests.Method</c>) is still declared under <c>tests/</c> — the anchors are
/// the acceptance evidence, and a renamed or deleted test otherwise rots silently — and no document
/// cites <c>AGENTS.md</c> by LINE NUMBER, because a line number drifts with every edit above it while
/// the rule's text does not. Historical red/green logs are excused only through
/// <c>docs/evidence/ticket-anchor-exceptions.json</c>, which the gate also keeps honest: an exception
/// nothing cites any more fails.
/// </summary>
public class BacklogIntegrityGateTests
{
	private const string IndexPath = "docs/backlog/README.md";
	private const string ExceptionsPath = "docs/evidence/ticket-anchor-exceptions.json";

	/// <summary>The index is read to pick the next work item, so a row is one scannable pointer: title, the ticket's priority, one clause saying what the ticket IS. A row that grows back into a summary is the drift this budget catches.</summary>
	internal const int IndexLineBudget = 160;

	/// <summary>A floor on the ROW CENSUS, not on the failures: the index carries every ticket, so a parser that silently stopped matching would otherwise pass by checking nothing.</summary>
	internal const int IndexRowFloor = 130;

	/// <summary>Folder name → the <c>- Status:</c> label that folder implies, and the index section that owns it. Internal because the cross-reference gate derives its status alternation from this one list instead of copying it.</summary>
	internal static readonly Dictionary<string, string> StatusOfFolder = new(StringComparer.Ordinal)
	{
		["todo"] = "Todo",
		["review"] = "Review",
		["in-progress"] = "In progress",
		["done"] = "Done",
		["resolved"] = "Resolved",
		["future"] = "Future",
		["watchlist"] = "Watchlist"
	};

	/// <summary>Point-in-time records are deliberately NOT anchor-checked: a selfcheck, an audit or a closed ticket is the record of what was true when it was written, so a test renamed afterwards must not force history to be rewritten. Everything else under <c>docs/</c> — the live backlog folders, the decisions register, the architecture specs, the evidence pages — is gated, which is where a dead anchor means a live claim pointing at nothing. Internal because the backlog cross-reference gate exempts exactly the same records.</summary>
	internal static readonly string[] RecordPrefixes = ["backlog/done/", "backlog/resolved/", "evidence/selfchecks/", "history/"];

	/// <summary>Every markdown document under <c>docs/</c> except the records above is anchor-checked: an evidence page that points at a test nobody declares is the same rot one directory over.</summary>
	private static IEnumerable<(string Folder, string File, string Text)> DocumentTexts() =>
		Directory.EnumerateFiles(RepositoryPaths.File("docs"), "*.md", SearchOption.AllDirectories)
			.Select(path => Path.GetRelativePath(RepositoryPaths.File("docs"), path).Replace('\\', '/'))
			.Where(relative => !RecordPrefixes.Any(prefix => relative.StartsWith(prefix, StringComparison.Ordinal)))
			.Select(relative => (Folder: relative.Split('/')[0], File: "docs/" + relative, Text: File.ReadAllText(RepositoryPaths.File("docs/" + relative))));

	/// <summary>Any link in the index must resolve; only a LIST ITEM or a TABLE ROW is an index entry (prose cross-references are not listings).</summary>
	private static readonly Regex AnyIndexLink = new(@"\]\(((?:todo|review|in-progress|done|resolved|future|watchlist)/[^)]+\.md)\)");

	private static readonly Regex IndexEntryLine = new(@"(?m)^(?:\s*-\s+\[|\s*\|)");
	private static readonly Regex IndexSection = new(@"(?m)^### (.+?)\s*$");
	private static readonly Regex StatusField = new(@"(?m)^- Status:[ \t]*(.+?)\s*$");
	/// <summary>A row's priority is the ticket's own field, so the bold token is the row's second column and the first whitespace-delimited token of <c>- Priority:</c> is the ticket's.</summary>
	private static readonly Regex IndexPriority = new(@"\*\*(.+?)\*\*");
	private static readonly Regex PriorityField = new(@"(?m)^- Priority:[ \t]*(.+?)\s*$");
	/// <summary>Test methods are PascalCase, which also keeps a file-name token such as <c>`FooTests.cs`</c> from being read as an anchor. Only the <c>Type.Method</c> form is gated: the shorthand <c>.Method</c> form is ambiguous with source-field mentions (`.INT`, `.trapRarityMultiplier`), so those tokens are not checked.</summary>
	private static readonly Regex TicketAnchor = new(@"`([A-Z][A-Za-z0-9_]*(?:Tests|Test))\.([A-Z][A-Za-z0-9_]*)`");
	/// <summary>Both shapes count: <c>AGENTS.md:327</c> and the backticked <c>`AGENTS.md` line 327</c>. A bare number after the name is NOT a citation — a byte count reads the same way.</summary>
	private static readonly Regex AgentsLineCitation = new(@"AGENTS\.md`?\s*(?::|line\s+)\s*\d+", RegexOptions.IgnoreCase);
	private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

	[Fact]
	public void EveryTicketIsIndexedExactlyOnceUnderItsOwnSectionAndEveryLinkResolves()
	{
		var failures = IndexFailures(RepositoryPaths.ReadText(IndexPath), TicketFilePaths());
		Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
	}

	[Fact]
	public void EveryIndexRowIsAPointerWithinItsBudgetCarryingItsTicketsPriority()
	{
		var rows = IndexRows(RepositoryPaths.ReadText(IndexPath));
		Assert.True(rows.Count >= IndexRowFloor, $"the index only parsed as {rows.Count} rows; the pointer rule would pass by checking almost nothing");

		var failures = IndexRowFailures(rows, TicketPriorities());
		Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
	}

	[Fact]
	public void TheStatusFoldersAreTheOnlyOnesAndNoTicketFileSitsLoose()
	{
		var failures = new List<string>();
		foreach (var directory in Directory.EnumerateDirectories(RepositoryPaths.File("docs/backlog")))
		{
			var name = Path.GetFileName(directory);
			if (!StatusOfFolder.ContainsKey(name))
			{
				failures.Add($"docs/backlog/{name}/ is not a known status folder, so nothing in it is indexed, status-checked or anchor-checked");
			}
		}

		foreach (var loose in Directory.EnumerateFiles(RepositoryPaths.File("docs/backlog"), "*.md"))
		{
			if (!string.Equals(Path.GetFileName(loose), "README.md", StringComparison.Ordinal))
			{
				failures.Add($"docs/backlog/{Path.GetFileName(loose)} sits outside every status folder and is therefore enumerated by nothing");
			}
		}

		Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
	}

	[Fact]
	public void EveryTicketStatusFieldAgreesWithItsFolder()
	{
		var failures = StatusFailures(TicketTexts());
		Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
	}

	[Fact]
	public void EveryDocumentedTestAnchorIsStillDeclared()
	{
		var scan = AnchorFailures(DocumentTexts(), DeclaredTestMembers(), LoadAnchorExceptions());
		Assert.True(scan.Failures.Count == 0, string.Join(Environment.NewLine, scan.Failures));

		// A floor on the CENSUS, not on the failures: if the scope silently shrinks (a prefix typo, a
		// renamed folder), the anchor rule would pass by checking almost nothing.
		Assert.True(scan.Citations >= 150, $"the anchor rule only saw {scan.Citations} citations; the gated documents carry far more");
	}

	[Fact]
	public void NoDocumentCitesAgentsMdByLineNumber()
	{
		var documents = Directory
			.EnumerateFiles(RepositoryPaths.File("docs"), "*.md", SearchOption.AllDirectories)
			.Select(path => (Relative: Path.GetRelativePath(RepositoryPaths.Root, path).Replace('\\', '/'), Text: File.ReadAllText(path)))
			.Append(("AGENTS.md", RepositoryPaths.ReadText("AGENTS.md")));
		var failures = CitationFailures(documents);
		Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
	}

	[Fact]
	public void IndexFailures_FindAMissingTargetADuplicateAndAMisplacedLink()
	{
		const string readme = """
			### Todo

			- [one](todo/one.md)
			- [two](todo/two.md)

			### Review

			- [three](todo/three.md)
			- [one again](todo/one.md)
			""";

		var failures = IndexFailures(readme, ["todo/one.md", "todo/two.md", "todo/three.md", "review/four.md"]);

		Assert.Contains(failures, f => f.Contains("review/four.md", StringComparison.Ordinal));
		Assert.Contains(failures, f => f.Contains("todo/one.md appears 2 time(s)", StringComparison.Ordinal));
		Assert.Contains(failures, f => f.Contains("todo/three.md", StringComparison.Ordinal) && f.Contains("Review", StringComparison.Ordinal));
	}

	[Fact]
	public void IndexRowFailures_FindAnOverlongRowAndAPriorityThatDisagrees()
	{
		const string readme = """
			### Todo

			- [one](todo/one.md) — **High** — a short pointer row.
			- [two](todo/two.md) — **Low** — this row is padded until it is far longer than the row budget, which is what the pointer rule exists to catch before an index turns back into a hand-maintained second copy of its tickets.
			- [three](todo/three.md) — **Low** — the ticket declares no priority, so this invents one.
			""";

		var rows = IndexRows(readme);
		var priorities = new Dictionary<string, string?>(StringComparer.Ordinal)
		{
			["todo/one.md"] = "Medium",
			["todo/two.md"] = "Medium",
			["todo/three.md"] = null
		};

		var failures = IndexRowFailures(rows, priorities);

		Assert.Contains(failures, f => f.Contains("todo/one.md", StringComparison.Ordinal) && f.Contains("Medium", StringComparison.Ordinal));
		Assert.Contains(failures, f => f.Contains("todo/two.md", StringComparison.Ordinal) && f.Contains("over the", StringComparison.Ordinal));
		Assert.Contains(failures, f => f.Contains("todo/three.md", StringComparison.Ordinal) && f.Contains("'(none)'", StringComparison.Ordinal));
		Assert.DoesNotContain(failures, f => f.Contains("todo/one.md", StringComparison.Ordinal) && f.Contains("over the", StringComparison.Ordinal));
	}

	[Fact]
	public void StatusFailures_FindAMissingAndAMismatchedField()
	{
		var failures = StatusFailures(
		[
			("todo", "todo/one.md", "- Status: Todo\n"),
			("review", "review/two.md", "- Status: Todo\n"),
			("review", "review/three.md", "no status line here\n")
		]);

		Assert.Contains(failures, f => f.Contains("review/two.md", StringComparison.Ordinal));
		Assert.Contains(failures, f => f.Contains("review/three.md", StringComparison.Ordinal));
		Assert.DoesNotContain(failures, f => f.Contains("todo/one.md", StringComparison.Ordinal));
	}

	[Fact]
	public void AnchorFailures_FindAnUndeclaredAnchorAndAStaleException()
	{
		var declared = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
		{
			["ThingTests"] = ["Exists_IsPinned"]
		};

		var failures = AnchorFailures(
			[("review", "review/one.md", "`ThingTests.Exists_IsPinned` and `ThingTests.WentAway`")],
			declared,
			new HashSet<string>(StringComparer.Ordinal) { "review/one.md|ThingTests.NeverCited" }).Failures;

		Assert.Contains(failures, f => f.Contains("ThingTests.WentAway", StringComparison.Ordinal));
		Assert.Contains(failures, f => f.Contains("stale anchor exception", StringComparison.Ordinal) && f.Contains("NeverCited", StringComparison.Ordinal));
	}

	[Fact]
	public void AnchorFailures_AcceptAnExcusedHistoricalAnchor()
	{
		var scan = AnchorFailures(
			[("review", "review/one.md", "`ThingTests.WentAway`")],
			new Dictionary<string, HashSet<string>>(StringComparer.Ordinal),
			new HashSet<string>(StringComparer.Ordinal) { "review/one.md|ThingTests.WentAway" });

		Assert.Empty(scan.Failures);
	}

	[Fact]
	public void CitationFailures_CatchBothShapes()
	{
		var failures = CitationFailures(
		[
			("docs/one.md", "see `AGENTS.md` line 327 for the rule"),
			("docs/two.md", "AGENTS.md:313 records the gate"),
			("docs/three.md", "AGENTS.md Development Workflow step 8 states it")
		]);

		Assert.Equal(2, failures.Count);
		Assert.Contains(failures, f => f.Contains("docs/one.md", StringComparison.Ordinal));
		Assert.Contains(failures, f => f.Contains("docs/two.md", StringComparison.Ordinal));
	}

	internal static List<string> IndexFailures(string readme, IReadOnlyCollection<string> files)
	{
		var failures = new List<string>();
		var entries = Entries(readme);

		foreach (var link in AnyIndexLink.Matches(readme).Select(match => match.Groups[1].Value).Distinct(StringComparer.Ordinal))
		{
			if (!files.Contains(link))
			{
				failures.Add($"the index links '{link}', which is not a ticket file under docs/backlog");
			}
		}

		foreach (var file in files)
		{
			var count = entries.Count(link => string.Equals(link, file, StringComparison.Ordinal));
			if (count != 1)
			{
				failures.Add($"{file} appears {count} time(s) in the index; every ticket is listed exactly once");
			}
		}

		foreach (var (section, body) in Sections(readme))
		{
			var folder = StatusOfFolder.FirstOrDefault(pair => string.Equals(pair.Key, section, StringComparison.OrdinalIgnoreCase) || string.Equals(pair.Value, section, StringComparison.OrdinalIgnoreCase)).Key;
			if (folder is null)
			{
				continue;
			}

			foreach (var path in Entries(body))
			{
				if (!path.StartsWith(folder + "/", StringComparison.Ordinal))
				{
					failures.Add($"{path} is listed under the '{section}' section but lives in {path.Split('/')[0]}/");
				}
			}
		}

		return failures;
	}

	/// <summary>Index rows are parsed exactly the way a reader scans them: the first ticket link on a list-item line, its bold priority token, and the line as written (the budget is a property of the file, not of the parsed fields).</summary>
	internal static List<IndexRow> IndexRows(string readme)
	{
		var rows = new List<IndexRow>();
		foreach (var line in readme.Split('\n'))
		{
			if (!IndexEntryLine.IsMatch(line))
			{
				continue;
			}

			var link = AnyIndexLink.Match(line);
			if (!link.Success)
			{
				continue;
			}

			var priority = IndexPriority.Match(line);
			rows.Add(new IndexRow(link.Groups[1].Value, line.TrimEnd(), priority.Success ? priority.Groups[1].Value : null));
		}

		return rows;
	}

	/// <summary>Two pointer rules, both aimed at the same rot: a row that re-states its ticket grows past the budget, and a row that repeats the ticket's priority drifts from it. A ticket that declares no priority (a closed record) must not have one invented for it in the index.</summary>
	internal static List<string> IndexRowFailures(IReadOnlyList<IndexRow> rows, IReadOnlyDictionary<string, string?> declared)
	{
		var failures = new List<string>();
		foreach (var row in rows)
		{
			if (row.Line.Length > IndexLineBudget)
			{
				failures.Add($"{row.Path}: the index row is {row.Line.Length} characters, over the {IndexLineBudget}-character budget; a row is a pointer, not a summary");
			}

			if (!declared.TryGetValue(row.Path, out var priority))
			{
				failures.Add($"{row.Path}: the index row has no ticket file to take a priority from");
				continue;
			}

			var expected = priority?.Split([' ', '\t'])[0];
			if (!string.Equals(expected, row.Priority, StringComparison.Ordinal))
			{
				failures.Add($"{row.Path}: the row carries priority '{row.Priority ?? "(none)"}' but the ticket declares '{priority ?? "(none)"}'");
			}
		}

		return failures;
	}

	/// <summary>Ticket path → the priority its <c>- Priority:</c> field declares, or null when it declares none. Only the first token is compared, so a parenthetical provenance note stays in the ticket.</summary>
	private static Dictionary<string, string?> TicketPriorities() => TicketTexts()
		.ToDictionary(ticket => ticket.File, ticket => PriorityField.Match(ticket.Text) is { Success: true } match ? match.Groups[1].Value.Trim() : null, StringComparer.Ordinal);

	internal sealed record IndexRow(string Path, string Line, string? Priority);

	/// <summary>Index entries are the FIRST link on a list-item line or on a table row; a second link on such a line (or one inside a sentence) is a cross-reference, not a listing.</summary>
	private static List<string> Entries(string text)
	{
		var entries = new List<string>();
		foreach (var line in text.Split('\n'))
		{
			if (!IndexEntryLine.IsMatch(line))
			{
				continue;
			}

			var match = AnyIndexLink.Match(line);
			if (match.Success)
			{
				entries.Add(match.Groups[1].Value);
			}
		}

		return entries;
	}

	internal static List<string> StatusFailures(IEnumerable<(string Folder, string File, string Text)> tickets)
	{
		var failures = new List<string>();
		foreach (var (folder, file, text) in tickets)
		{
			var match = StatusField.Match(text);
			var expected = StatusOfFolder[folder];
			if (!match.Success)
			{
				failures.Add($"{file}: no '- Status:' line (the folder says {expected})");
			}
			else if (!match.Groups[1].Value.StartsWith(expected, StringComparison.Ordinal))
			{
				failures.Add($"{file}: '- Status: {match.Groups[1].Value}' disagrees with its folder ({expected})");
			}
		}

		return failures;
	}

	internal static AnchorScan AnchorFailures(
		IEnumerable<(string Folder, string File, string Text)> tickets,
		IReadOnlyDictionary<string, HashSet<string>> declared,
		IReadOnlySet<string> exceptions)
	{
		var failures = new List<string>();
		var used = new HashSet<string>(StringComparer.Ordinal);
		var citations = 0;

		foreach (var (_, file, text) in tickets)
		{
			foreach (Match match in TicketAnchor.Matches(text))
			{
				citations++;
				var anchor = match.Groups[1].Value + "." + match.Groups[2].Value;
				if (declared.TryGetValue(match.Groups[1].Value, out var members) && members.Contains(match.Groups[2].Value))
				{
					continue;
				}

				var key = file + "|" + anchor;
				if (exceptions.Contains(key))
				{
					used.Add(key);
					continue;
				}

				failures.Add($"{file}: '{anchor}' is not declared under tests/ (renamed, deleted, or mis-copied)");
			}
		}

		foreach (var exception in exceptions.Where(exception => !used.Contains(exception)))
		{
			failures.Add($"stale anchor exception (no ticket cites it any more): {exception}");
		}

		return new AnchorScan(failures, used, citations);
	}

	internal static List<string> CitationFailures(IEnumerable<(string File, string Text)> documents)
	{
		var failures = new List<string>();
		foreach (var (file, text) in documents)
		{
			foreach (Match match in AgentsLineCitation.Matches(text))
			{
				failures.Add($"{file}: '{match.Value}' cites AGENTS.md by line number; cite the section name and quote the text instead");
			}
		}

		return failures;
	}

	internal sealed record AnchorScan(List<string> Failures, HashSet<string> UsedExceptions, int Citations);

	private static IEnumerable<(string Section, string Body)> Sections(string readme)
	{
		var matches = IndexSection.Matches(readme);
		for (var i = 0; i < matches.Count; i++)
		{
			var start = matches[i].Index + matches[i].Length;
			var end = i + 1 < matches.Count ? matches[i + 1].Index : readme.Length;
			yield return (matches[i].Groups[1].Value.Trim(), readme[start..end]);
		}
	}

	private static IReadOnlyCollection<string> TicketFilePaths() =>
		[.. TicketFiles().Select(file => file.Folder + "/" + Path.GetFileName(file.Path))];

	private static IEnumerable<(string Folder, string File, string Text)> TicketTexts() => TicketFiles()
		.Select(file => (file.Folder, file.Folder + "/" + Path.GetFileName(file.Path), File.ReadAllText(file.Path)));

	private static IEnumerable<(string Folder, string Path)> TicketFiles() => StatusOfFolder.Keys
		.SelectMany(folder => Directory.Exists(RepositoryPaths.File("docs/backlog/" + folder))
			? Directory.EnumerateFiles(RepositoryPaths.File("docs/backlog/" + folder), "*.md").Select(path => (Folder: folder, Path: path))
			: []);

	private static Dictionary<string, HashSet<string>> DeclaredTestMembers()
	{
		var declared = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
		foreach (var path in Directory.EnumerateFiles(RepositoryPaths.File("tests"), "*.cs", SearchOption.AllDirectories))
		{
			if (path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
				path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
			{
				continue;
			}

			var root = CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetRoot();
			foreach (var type in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
			{
				if (!declared.TryGetValue(type.Identifier.ValueText, out var members))
				{
					members = [];
					declared[type.Identifier.ValueText] = members;
				}

				foreach (var method in type.Members.OfType<MethodDeclarationSyntax>())
				{
					members.Add(method.Identifier.ValueText);
				}
			}
		}

		return declared;
	}

	private static HashSet<string> LoadAnchorExceptions()
	{
		var file = JsonSerializer.Deserialize<AnchorExceptionFile>(RepositoryPaths.ReadText(ExceptionsPath), JsonOptions);
		return file?.Exceptions is null
			? []
			: file.Exceptions.Select(entry => entry.Ticket + "|" + entry.Anchor).ToHashSet(StringComparer.Ordinal);
	}

	private sealed record AnchorExceptionFile(List<AnchorException>? Exceptions);

	private sealed record AnchorException(string Ticket, string Anchor);

	/// <summary>
	/// A relative link inside a document must resolve. A ticket moving between status folders — or a
	/// renamed file — otherwise leaves a pointer only a reader discovers: measured 2026-09-21, moving
	/// one ticket from <c>todo/</c> to <c>review/</c> broke its two outbound links while the whole
	/// gate set stayed green, because the index gate resolves the index's own links only. Code spans
	/// are removed before scanning, because the index format itself is documented inside backticks.
	/// </summary>
	[Fact]
	public void EveryRelativeDocumentLink_Resolves()
	{
		var broken = new List<string>();
		var checkedLinks = 0;
		foreach (var (_, file, text) in DocumentTexts())
		{
			foreach (Match match in LinkTarget.Matches(WithoutCodeSpans(text)))
			{
				var link = match.Groups[1].Value.Trim();
				if (link.StartsWith('#') || Uri.IsWellFormedUriString(link, UriKind.Absolute))
				{
					continue;
				}

				var target = link.Split('#')[0];
				if (target.Length == 0)
				{
					continue;
				}

				checkedLinks++;
				var directory = Path.GetDirectoryName(RepositoryPaths.File(file))!;
				var resolved = Path.GetFullPath(Path.Combine(directory, target));
				if (!File.Exists(resolved) && !Directory.Exists(resolved))
				{
					broken.Add(file + " -> " + link);
				}
			}
		}

		Assert.True(checkedLinks >= RelativeLinkFloor,
			$"link census floor: checked {checkedLinks} relative link(s), expected at least {RelativeLinkFloor} — a scan that matches nothing must not pass.");
		Assert.True(broken.Count == 0, "dead relative link(s):" + Environment.NewLine + string.Join(Environment.NewLine, broken));
	}

	/// <summary>The scan's matcher, self-tested below so a later edit cannot silently narrow it.</summary>
	[Fact]
	public void RelativeLinkScan_SkipsCodeSpans_AndKeepsRealLinks()
	{
		const string Sample = "the row format is `- [Title](path) — one clause`; see [the ticket](../todo/x.md) and [the site](https://example.com/x).";

		var found = LinkTarget.Matches(WithoutCodeSpans(Sample)).Select(match => match.Groups[1].Value).ToArray();

		string[] expected = ["../todo/x.md", "https://example.com/x"];
		Assert.Equal(expected, found);
	}

	/// <summary>Measured 2026-09-21: 370 relative links under <c>docs/</c>; the floor is about two thirds of that, so a scan that stopped matching links fails instead of passing quietly.</summary>
	internal const int RelativeLinkFloor = 250;

	private static readonly Regex CodeSpan = new("`[^`]*`", RegexOptions.Compiled);

	private static readonly Regex LinkTarget = new(@"\]\(([^)]+)\)", RegexOptions.Compiled);

	private static string WithoutCodeSpans(string text) => CodeSpan.Replace(text, string.Empty);

}
