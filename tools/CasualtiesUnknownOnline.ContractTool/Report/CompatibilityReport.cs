using System.Collections.Generic;
using System.Linq;
using System.Text;
using CasualtiesUnknownOnline.ContractTool.Diff;
using CasualtiesUnknownOnline.ContractTool.Snapshot;

namespace CasualtiesUnknownOnline.ContractTool.Report;

/// <summary>
/// Renders the classified diff as the update-day compatibility report: the
/// verdict first, then the contract tiers, then what sits outside the lens.
///
/// The report is deterministic — no timestamp, no machine path, nothing about
/// the run — so a report can be diffed against another and a checked-in
/// expectation is meaningful. It also states its own limits (structural only;
/// the hand-declared dynamic contracts are not in the lens), because a report
/// that overstates its reach is worse than a short one.
/// </summary>
public static class CompatibilityReport
{
	private const int OutsideLensListingCap = 25;

	/// <summary>Renders the markdown report for a comparison.</summary>
	public static string Render(DiffResult result)
	{
		var builder = new StringBuilder();
		builder.AppendLine("# Game-assembly contract report");
		builder.AppendLine();
		AppendBuild(builder, "Previous build", result.Previous);
		AppendBuild(builder, "Current build", result.Current);
		builder.AppendLine($"- Snapshot schema: `{SnapshotSchema.Snapshot}`");
		builder.AppendLine($"- Contract lens (previous): {Describe(result.PreviousLens)}");
		builder.AppendLine($"- Contract lens (current): {Describe(result.CurrentLens)}");
		builder.AppendLine();
		AppendVerdict(builder, result);
		AppendSummary(builder, result);
		AppendScope(builder, result, DifferenceScope.Contract);
		AppendScope(builder, result, DifferenceScope.ContractAdjacent);
		AppendScope(builder, result, DifferenceScope.OutsideContract);
		AppendLimits(builder);
		return builder.ToString();
	}

	private static void AppendBuild(StringBuilder builder, string label, SnapshotDocument document)
	{
		var counts = document.Counts;
		builder.AppendLine(
			$"- {label}: `{document.Assembly.Name}` {document.Assembly.Version} — sha256 `{document.Assembly.Sha256}` "
			+ $"({counts.Types} types, {counts.Methods} methods, {counts.Fields} fields, {counts.EnumMembers} enum members, {counts.SerializedFields} serialized fields)");
	}

	private static string Describe(ContractLens lens) =>
		$"{lens.Total} rows — {lens.Resolved} resolved, {lens.Ambiguous} ambiguous, {lens.Unresolved} unresolved";

	private static void AppendVerdict(StringBuilder builder, DiffResult result)
	{
		var broken = result.Differences.Count(difference =>
			difference.Scope == DifferenceScope.Contract && difference.Kind != DifferenceKind.UnchangedNeedsReview);
		builder.AppendLine("## Verdict");
		builder.AppendLine();
		builder.AppendLine(broken == 0
			? "**No contract-breaking difference.** Every patch target the lens could resolve still resolves; the rows under \"Unchanged, still needs a semantic look\" are what the structural half cannot clear."
			: $"**{broken} contract-breaking difference(s).** Each row under \"Contract verdicts\" names a hook whose target moved — fix or retarget it before the build can patch.");
		builder.AppendLine();
	}

	private static void AppendSummary(StringBuilder builder, DiffResult result)
	{
		builder.AppendLine("| Classification | Count |");
		builder.AppendLine("|---|---|");
		foreach (var kind in new[]
		{
			DifferenceKind.RemovedOrRenamed,
			DifferenceKind.SignatureChanged,
			DifferenceKind.HarmonyTargetAmbiguous,
			DifferenceKind.FieldShapeChanged,
			DifferenceKind.EnumValueChanged,
			DifferenceKind.UnchangedNeedsReview,
			DifferenceKind.Added,
		})
		{
			builder.AppendLine($"| {DifferenceVocabulary.Label(kind)} | {result.Count(kind)} |");
		}

		builder.AppendLine();
	}

	private static void AppendScope(StringBuilder builder, DiffResult result, DifferenceScope scope)
	{
		var differences = result.InScope(scope).ToList();
		builder.AppendLine($"## {DifferenceVocabulary.Label(scope)} ({differences.Count})");
		builder.AppendLine();
		if (differences.Count == 0)
		{
			builder.AppendLine("None.");
			builder.AppendLine();
			return;
		}

		foreach (var group in differences.GroupBy(difference => difference.Kind))
		{
			var rows = group.ToList();
			builder.AppendLine($"### {DifferenceVocabulary.Label(group.Key)} ({rows.Count})");
			builder.AppendLine();
			if (group.Key == DifferenceKind.UnchangedNeedsReview)
			{
				foreach (var row in rows)
				{
					builder.AppendLine($"- `{row.Subject}` — `{row.Current}`");
				}
			}
			else
			{
				AppendTable(builder, rows);
			}

			builder.AppendLine();
		}
	}

	private static void AppendTable(StringBuilder builder, IReadOnlyList<ContractDifference> rows)
	{
		var listed = rows.Count > OutsideLensListingCap ? [.. rows.Take(OutsideLensListingCap)] : rows;
		builder.AppendLine("| Subject | Previous | Current | Detail |");
		builder.AppendLine("|---|---|---|---|");
		foreach (var row in listed)
		{
			builder.AppendLine($"| `{row.Subject}` | {Cell(row.Previous)} | {Cell(row.Current)} | {Cell(row.Detail)} |");
		}

		if (listed.Count < rows.Count)
		{
			builder.AppendLine();
			builder.AppendLine($"- … and {rows.Count - listed.Count} more of this classification in the JSON (`diff --json`).");
		}
	}

	private static string Cell(string value) => value.Replace("|", "\\|");

	private static void AppendLimits(StringBuilder builder)
	{
		builder.AppendLine("## Limits");
		builder.AppendLine();
		builder.AppendLine("- **Structural only.** This report answers whether the SHAPE moved. Whether a member that is shaped the same still MEANS the same thing needs the live game and stays in `future/adapter-shell-verification-harness.md`; neither half substitutes for the other.");
		builder.AppendLine("- **The hand-declared dynamic contracts are not in this lens.** `PatchInventory` declares nine contracts by hand (their patch-class names carry `\"(dynamic)\"`) and there is no attribute for metadata to read. They are covered by the patch-contract tests and by the update-day runbook's contract-test step.");
		builder.AppendLine("- **A contract whose target type is outside the snapshotted assembly has no rows here.** A snapshot covers one assembly; a hook on a Unity type (for example `SceneManager.LoadScene`) reports as unresolved in the lens, and the patch-contract tests — which resolve against every referenced assembly — are what judge it.");
		builder.AppendLine("- **Enum member ORDER is not compared** (values are), and no method body is compared: a method whose body changed keeps its signature and, when a contract targets it, lands under \"unchanged, still needs a semantic look\".");
		builder.AppendLine("- **Compiler-generated members are included** and flagged `isCompilerGenerated`: a member the game reads inside a hooked method can live in a lambda no contract can name (decision 199's residual).");
	}
}
