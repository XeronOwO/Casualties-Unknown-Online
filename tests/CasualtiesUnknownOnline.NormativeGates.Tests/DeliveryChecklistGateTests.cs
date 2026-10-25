using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// The delivery checklist — <c>docs/evidence/delivery-checklist.md</c> — is the process record every
/// development cycle fills, and the gate is what makes it binding: an open required box refuses the
/// cycle's final commit, and a checked honey-pot fails on purpose. Its census is the other half of that
/// job: the steps the process requires are named here and must still be DECLARED, so a checklist that
/// lost its boxes — a bad edit, a "clean-up" that dropped a step — fails by the NAME of the step it lost
/// instead of passing with nothing left to fail on. A green run proves the record's shape; whether a step
/// was really run stays the review's question.
/// </summary>
public sealed class DeliveryChecklistGateTests
{
	private const string ChecklistPath = "docs/evidence/delivery-checklist.md";

	/// <summary>
	/// The boxes the process requires the checklist to keep declaring, by the LEADING text of the item —
	/// the stable half of the line. Every cycle rewrites the tail of each box it runs (the evidence
	/// suffix) and flips its checkbox, so a census keyed on the whole line would fail each time a cycle
	/// recorded itself; keyed on the leading text it survives, and a checklist whose boxes are gone
	/// reports every required step by name instead of passing with nothing left to fail on. This list IS
	/// the floor: a cycle that ADDS a box needs no edit here, while one that drops or renames a step edits
	/// this list deliberately — which is the act the gate exists to force. Order is not enforced: a cycle
	/// moves its boxes for its own reasons, and the process order is the workflow's business rather than
	/// this census's.
	/// </summary>
	private static readonly string[] RequiredChecklistItems =
	[
		"Design comprehension",
		"Native parity",
		"Native parity probe",
		"Entry mapping",
		"Mechanism inventory",
		"Whole-family audit",
		"Self-check table",
		"Verification design",
		"Plan approved by the user",
		"Build + dotnet format + dotnet test normative gates pass",
		"Structure review done",
		"Release-cycle deployment/acceptance",
		"FORBIDDEN"
	];

	/// <summary>The two boxes that stand open by design: the post-commit acceptance reminder, which is
	/// never checked before a commit, and the honey-pot, which fails when it IS checked.</summary>
	private const string AcceptanceChecklistItem = "Release-cycle deployment/acceptance";
	private const string ForbiddenChecklistItem = "FORBIDDEN";

	/// <summary>The marker a checked box's evidence suffix follows; the failure messages cut at it.</summary>
	private const string ChecklistEvidenceMarker = " — evidence:";

	/// <summary>
	/// The checklist is validated by `tools/check-delivery.ps1`, run before a commit, NOT by a test: a test cannot reset
	/// the file after it validates it, and a file left green certifies the next cycle without a record — which is exactly
	/// what happened on 2026-10-25 (a whole session of commits ran against a previous cycle's checked boxes). The test
	/// keeps only the half a test can reach: the checklist still DECLARES every required step, so the script can never
	/// pass a file that lost one.
	/// </summary>
	/// <summary>The census the checklist gate used to lack: a checklist with nothing declared has nothing
	/// left to fail on, so it passed. Every required step is reported by name instead — which is also why
	/// the floor is the item list rather than a number: a count that moves with the cycle says nothing
	/// about WHICH step went missing.</summary>
	[Fact]
	public void DeliveryChecklist_DeclaresEveryRequiredBox()
	{
		var failures = MissingRequiredBoxFailures(ChecklistBoxes(ChecklistText()));
		Assert.True(failures.Length == 0, Describe("the delivery checklist's declared boxes", failures));
	}

	// --- the checklist's own contract: each sample feeds synthetic text to the same pure functions the
	// facts use, so a reader that starts accepting prose, a census that stops counting and a standing box
	// that stops being recognised all fail here ---

	[Fact]
	public void TheChecklistReader_ReadsBoxLinesAndNothingElse()
	{
		var boxes = ChecklistBoxes("""
			Prose that mentions - [ ] an inline marker, and a bullet list:
			- a plain bullet, which is not a box
			* [ ] a different bullet syntax
			- [x] Entry mapping (a scope that crosses players): the item — evidence: not applicable
			- [X] Structure review done: an uppercase marker is a checked box
			- [ ] FORBIDDEN — never check this box
			""");
		Assert.Equal(3, boxes.Length);
		Assert.True(boxes[0].Checked);
		Assert.StartsWith("Entry mapping", boxes[0].Item, StringComparison.Ordinal);
		Assert.True(boxes[1].Checked);
		Assert.StartsWith("Structure review done", boxes[1].Item, StringComparison.Ordinal);
		Assert.False(boxes[2].Checked);
		Assert.Equal("FORBIDDEN — never check this box", boxes[2].Item);
	}

	[Fact]
	public void TheRequiredBoxCensus_FlagsADeletedOrRenamedStep()
	{
		// The state the ticket exists for: the file is prose, and every required step is reported by name
		// rather than passing because nothing is left to fail on.
		var failures = MissingRequiredBoxFailures(ChecklistBoxes("# Delivery Checklist" + Environment.NewLine + "Every cycle runs through this checklist."));
		Assert.Equal(RequiredChecklistItems.Length, failures.Length);
		Assert.Contains(failures, failure => failure.Contains("`Entry mapping`", StringComparison.Ordinal));
		Assert.Contains(failures, failure => failure.Contains("`FORBIDDEN`", StringComparison.Ordinal));

		// Renaming a step is the same finding — and the standing file, boxes declared but none checked yet,
		// answers nothing missing: that is the state the census has to pass while the sibling fact refuses
		// the cycle for its open boxes.
		var declared = string.Join(Environment.NewLine, RequiredChecklistItems.Select(item => $"- [ ] {item}"));
		Assert.Contains(
			MissingRequiredBoxFailures(ChecklistBoxes(declared.Replace("- [ ] Entry mapping", "- [ ] Entry points", StringComparison.Ordinal))),
			failure => failure.Contains("`Entry mapping`", StringComparison.Ordinal));
		Assert.Empty(MissingRequiredBoxFailures(ChecklistBoxes(declared)));

		// One line cannot stand in for the list: the match is the box's LEADING text, so a single box naming
		// every step inside its prose still answers only the first — the census cannot be satisfied by fewer
		// boxes than the process has steps.
		Assert.Equal(
			RequiredChecklistItems.Length - 1,
			MissingRequiredBoxFailures(ChecklistBoxes("- [ ] " + string.Join(" ", RequiredChecklistItems))).Length);
	}

	[Fact]
	public void TheIncompleteBoxCheck_FlagsAnOpenStepAndACheckedHoneyPot()
	{
		// The mid-cycle state reports the steps not yet run and nothing else: the two standing boxes are
		// exempt, and the census stays silent about the same text.
		var declared = string.Join(Environment.NewLine, RequiredChecklistItems.Select(item => $"- [ ] {item}"));
		var boxes = ChecklistBoxes(declared);
		Assert.Equal(
			RequiredChecklistItems.Where(item => item != AcceptanceChecklistItem && item != ForbiddenChecklistItem),
			IncompleteBoxFailures(boxes));
		Assert.Empty(MissingRequiredBoxFailures(boxes));

		// A checked honey-pot is the one failure the standing exception still owes — with every box checked
		// (the acceptance reminder included, which is allowed) the honey-pot's own check is all that fails.
		var allChecked = string.Join(Environment.NewLine, RequiredChecklistItems.Select(item => $"- [x] {item}"));
		var potFailure = Assert.Single(IncompleteBoxFailures(ChecklistBoxes(allChecked)));
		Assert.Contains("honey-pot", potFailure, StringComparison.Ordinal);

		// Standing is the box's own item, not a word in its prose: an evidence suffix naming FORBIDDEN does
		// not exempt an open box, which is what the check this replaced did by testing `Contains` on the
		// whole line.
		var openStep = Assert.Single(IncompleteBoxFailures(ChecklistBoxes("- [ ] Mechanism inventory — evidence: the FORBIDDEN honey-pot still fails")));
		Assert.Contains("Mechanism inventory", openStep, StringComparison.Ordinal);
		Assert.DoesNotContain("evidence:", openStep, StringComparison.Ordinal);
	}

	/// <summary>The checklist's declared boxes: the checkbox state and the item after the marker. The item
	/// is kept whole — a required step matches the box's LEADING text, so the evidence suffix a cycle
	/// appends never has to be parsed out, and prose that merely names a step is not a box.</summary>
	private static ChecklistBox[] ChecklistBoxes(string checklistText) =>
		[.. checklistText
			.Split('\n')
			.Select(line => line.Trim())
			.Where(IsChecklistBox)
			.Select(line => new ChecklistBox(line[3] is 'x' or 'X', line[5..].Trim()))];

	private static bool IsChecklistBox(string line) =>
		line.StartsWith("- [ ] ", StringComparison.Ordinal)
		|| line.StartsWith("- [x] ", StringComparison.Ordinal)
		|| line.StartsWith("- [X] ", StringComparison.Ordinal);

	private static string ChecklistText()
	{
		var path = RepositoryPaths.File(ChecklistPath);
		Assert.True(File.Exists(path), "delivery checklist missing");
		return File.ReadAllText(path);
	}

	/// <summary>Every required step is still declared by a box of its own. The failure names the step, so a
	/// reader knows which one to restore — or which line of <see cref="RequiredChecklistItems"/> to drop
	/// deliberately; a step whose box was renamed away reads as missing, which is the point.</summary>
	private static string[] MissingRequiredBoxFailures(IReadOnlyList<ChecklistBox> boxes) =>
		[.. RequiredChecklistItems
			.Where(required => !boxes.Any(box => box.Item.StartsWith(required, StringComparison.Ordinal)))
			.Select(required => $"no box declares `{required}` — restore the box, or drop it from RequiredChecklistItems deliberately if the process no longer runs the step")];

	/// <summary>An open box is a step this cycle has not run yet — the gate's normal mid-cycle red. The two
	/// standing boxes are the exception the checklist declares: the post-commit acceptance reminder, never
	/// checked before a commit, and the honey-pot, which fails when it IS checked.</summary>
	private static string[] IncompleteBoxFailures(IReadOnlyList<ChecklistBox> boxes)
	{
		var failures = new List<string>();
		foreach (var box in boxes)
		{
			if (box.Item.StartsWith(ForbiddenChecklistItem, StringComparison.Ordinal))
			{
				if (box.Checked)
				{
					failures.Add($"the honey-pot box is checked: {ChecklistLabel(box.Item)}");
				}

				continue;
			}

			if (!box.Checked && !box.Item.StartsWith(AcceptanceChecklistItem, StringComparison.Ordinal))
			{
				failures.Add(ChecklistLabel(box.Item));
			}
		}

		return [.. failures];
	}

	/// <summary>The item without the cycle's evidence suffix — what the box is, as opposed to what last
	/// proved it. Only the failure messages use it.</summary>
	private static string ChecklistLabel(string item)
	{
		var marker = item.IndexOf(ChecklistEvidenceMarker, StringComparison.Ordinal);
		return marker < 0 ? item : item[..marker];
	}

	private static string Describe(string check, IReadOnlyList<string> failures) =>
		$"{check}: {failures.Count} violation(s){Environment.NewLine}{string.Join(Environment.NewLine, failures)}";

	/// <summary>One declared box: its checkbox state and the item it names, evidence suffix included.</summary>
	private sealed record ChecklistBox(bool Checked, string Item);
}
