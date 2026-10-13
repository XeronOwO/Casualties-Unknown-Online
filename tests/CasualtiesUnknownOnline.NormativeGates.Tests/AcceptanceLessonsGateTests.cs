using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// The acceptance lessons ledger — <c>docs/acceptance/lessons.md</c> — is a list of pitfalls, not a
/// journal: every entry is a dated headline and ONE line naming what its reader can open or act on, and
/// the file stays inside its own budget so it cannot grow back into the append-only log the document
/// audit rewrote (<c>docs/backlog/review/document-corpus-audit.md</c>, required work item 3: "an entry is
/// bounded, says what its reader does with it, and names the page that carries its conclusion"). The
/// rule behind it is <c>docs/AGENTS.md</c> §6 — keep the distilled conclusion, delete the log — and the
/// page's own header states the same contract for whoever adds the next entry. A green run proves the
/// shape; it cannot judge whether a lesson is worth keeping, which is the review's question.
/// </summary>
public sealed class AcceptanceLessonsGateTests
{
	private const string LedgerPath = "docs/acceptance/lessons.md";

	/// <summary>Measured when the ledger was rewritten (2026-10-13): 117 entries, the largest entry line
	/// 940 characters, 59,861 bytes as the working copy holds it (CRLF). The line ceiling leaves room for
	/// a merged lesson and refuses the paragraph an entry used to grow into; the file budget is under the
	/// census's document budget (114,688 bytes), so this page can never again be the reason that budget is
	/// spent — and at the rewrite it sat at 91% of it, which is why the routing pages say a harvest folds
	/// its lesson where it belongs instead of appending one here.</summary>
	private const int EntryLineCeiling = 1200;
	private const int LedgerBudgetBytes = 65536;

	/// <summary>The entry count at the rewrite: a broken parse must fail loudly rather than report a
	/// clean ledger over an empty entry set.</summary>
	private const int EntryFloor = 100;

	[Fact]
	public void EveryEntry_IsAHeadlineAndExactlyOneLine()
	{
		var failures = ShapeFailures(File.ReadAllText(RepositoryPaths.File(LedgerPath)));
		Assert.True(failures.Count == 0, Describe("the ledger's entry shape", failures));
	}

	[Fact]
	public void EveryEntry_NamesSomethingItsReaderCanOpen()
	{
		var failures = CarrierFailures(File.ReadAllText(RepositoryPaths.File(LedgerPath)));
		Assert.True(failures.Count == 0, Describe("an entry's carrier", failures));
	}

	[Fact]
	public void TheLedger_KeepsItsEntries()
	{
		var failures = FloorFailures(Entries(File.ReadAllText(RepositoryPaths.File(LedgerPath))).Count);
		Assert.True(failures.Count == 0, Describe("the ledger's entry count", failures));
	}

	[Fact]
	public void TheLedger_StaysInsideItsBudget()
	{
		var failure = BudgetFailure(LedgerPath, new FileInfo(RepositoryPaths.File(LedgerPath)).Length);
		Assert.True(failure is null, failure);
	}

	// --- the shape's own contract: each sample feeds synthetic text to the same pure functions the
	// facts use, so a parser that starts accepting prose, a carrier check that stops checking and a
	// budget that stops biting all fail here ---

	[Fact]
	public void TheEntryParser_FlagsANarrative()
	{
		var text = """
			# Acceptance lessons

			## 2026-01-01 — A thing happened

			A rule that names `docs/acceptance/workflow.md`.

			- Symptom: it happened.
			- Cause: because.
			""";
		var failures = ShapeFailures(text);
		Assert.Contains(failures, failure => failure.Contains("2026-01-01", StringComparison.Ordinal));
		Assert.Empty(ShapeFailures("""
			# Acceptance lessons

			## 2026-01-01 — A thing happened

			A rule that names `docs/acceptance/workflow.md`.
			"""));
	}

	[Fact]
	public void TheEntryParser_FlagsASecondLineAndAnOverlongLine()
	{
		var twoLines = ShapeFailures("""
			## 2026-01-01 — A thing happened

			A rule that names `docs/acceptance/workflow.md`.
			And a second thought that should have been its own entry.
			""");
		Assert.Single(twoLines);

		var overlong = ShapeFailures($"## 2026-01-01 — A thing happened{Environment.NewLine}{Environment.NewLine}{new string('x', EntryLineCeiling + 1)} `docs/acceptance/workflow.md`");
		Assert.Single(overlong);
		Assert.Contains("characters", Assert.Single(overlong), StringComparison.Ordinal);

		var empty = ShapeFailures("## 2026-01-01 — A thing happened");
		Assert.Single(empty);
		Assert.Contains("no line saying", Assert.Single(empty), StringComparison.Ordinal);
	}

	[Fact]
	public void TheCarrierCheck_FlagsAnEntryThatNamesNothing()
	{
		Assert.Single(CarrierFailures("""
			## 2026-01-01 — A thing happened

			A rule with nothing to open behind it.
			"""));
		Assert.Empty(CarrierFailures("""
			## 2026-01-01 — A thing happened

			A rule that names `docs/acceptance/workflow.md`.
			"""));
	}

	[Fact]
	public void TheBudgetAndFloorChecks_Bite()
	{
		Assert.Contains("over its", BudgetFailure("docs/acceptance/lessons.md", LedgerBudgetBytes + 1), StringComparison.Ordinal);
		Assert.Null(BudgetFailure("docs/acceptance/lessons.md", LedgerBudgetBytes));
		Assert.Contains("below the floor", Assert.Single(FloorFailures(EntryFloor - 1)), StringComparison.Ordinal);
		Assert.Empty(FloorFailures(EntryFloor));
	}

	private static IReadOnlyList<Entry> Entries(string text)
	{
		var entries = new List<Entry>();
		string? heading = null;
		var body = new List<string>();
		foreach (var line in text.Split('\n'))
		{
			var trimmed = line.Trim();
			if (IsHeading(trimmed))
			{
				if (heading is not null)
				{
					entries.Add(new Entry(heading, string.Join("\n", body)));
				}

				heading = trimmed;
				body.Clear();
				continue;
			}

			if (heading is not null && trimmed.Length > 0)
			{
				body.Add(trimmed);
			}
		}

		if (heading is not null)
		{
			entries.Add(new Entry(heading, string.Join("\n", body)));
		}

		return entries;
	}

	/// <summary>An entry is a dated headline plus ONE line; that line carries the bounded statement of
	/// what the reader does with the lesson, and a bullet list or a second sentence is the narrative the
	/// rewrite deleted.</summary>
	private static IReadOnlyList<string> ShapeFailures(string text)
	{
		var failures = new List<string>();
		foreach (var entry in Entries(text))
		{
			if (entry.Body.Length == 0)
			{
				failures.Add($"{entry.Heading}: no line saying what its reader does with the lesson");
				continue;
			}

			var bodyLines = entry.Body.Split('\n');
			if (bodyLines.Length > 1)
			{
				failures.Add($"{entry.Heading}: {bodyLines.Length} lines — an entry is a headline and ONE line; a narrative belongs in the record it cites");
			}

			foreach (var line in bodyLines)
			{
				if (line.Length > EntryLineCeiling)
				{
					failures.Add($"{entry.Heading}: {line.Length} characters, over the {EntryLineCeiling}-character ceiling");
				}
			}
		}

		return failures;
	}

	/// <summary>Every entry names something its reader can open or act on — a page, a tool, a recipe, a
	/// test, a record, a log marker or a probe — spelled in backticks, so a claim without a carrier is a
	/// violation rather than a rule.</summary>
	private static IReadOnlyList<string> CarrierFailures(string text) =>
		[.. Entries(text)
			.Where(entry => !entry.Body.Contains('`'))
			.Select(entry => $"{entry.Heading}: names nothing its reader can open — put the carrier, the recipe, the log marker or the record in backticks")];

	private static IReadOnlyList<string> FloorFailures(int entries) =>
		entries >= EntryFloor
			? []
			: [$"the ledger carries {entries} entries, below the floor {EntryFloor} — the parser or the ledger broke"];

	private static string? BudgetFailure(string path, long size) =>
		size <= LedgerBudgetBytes
			? null
			: $"{path} is {size} bytes, over its {LedgerBudgetBytes}-byte budget: an entry whose conclusion has landed elsewhere is deleted, not appended";

	/// <summary>A dated headline is the only heading the ledger's entries use; the page's own sections
	/// ("How to add an entry") and the template inside its fenced block are not entries.</summary>
	private static bool IsHeading(string line) =>
		line.StartsWith("## ", StringComparison.Ordinal)
		&& line.Length > 14
		&& char.IsDigit(line[3]) && char.IsDigit(line[4]) && char.IsDigit(line[5]) && char.IsDigit(line[6])
		&& line[7] == '-' && line[10] == '-';

	private static string Describe(string check, IReadOnlyList<string> failures) =>
		$"{check}: {failures.Count} violation(s){Environment.NewLine}{string.Join(Environment.NewLine, failures)}";

	private sealed record Entry(string Heading, string Body);
}
