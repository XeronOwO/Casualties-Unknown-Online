using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// The run sheet is the execution contract (root <c>AGENTS.md</c>, Development Workflow rule 10): a cycle writes the
/// numbered steps into the ticket before acting, and a ticket may only be closed when every step is checked. This gate is
/// the mechanical half of that rule — the rule itself cannot see whether a cycle stopped early, but it can refuse a
/// ticket that claims Done while its own sheet still has an open box, which is the shape the failure always took
/// (a status message in place of the remaining steps).
///
/// <para>
/// Shape: a ticket that declares <c>## Run sheet</c> must carry at least one checkbox under it; a ticket in <c>done/</c>
/// (or whose Status begins with <c>Done</c>) must have no unchecked box left anywhere in the file. A ticket without a run
/// sheet is untouched by this gate: the sheet is required for work in flight, not for the historical record.
/// </para>
/// </summary>
[Trait("Category", "NormativeGate")]
public sealed class BacklogRunSheetGateTests
{
	private const string SheetHeading = "## Run sheet";

	[Fact]
	public void EveryTicketClosedUnderADoneStatus_HasNoOpenStepLeft()
	{
		var violations = new List<string>();
		foreach (var (path, text) in ReadTickets())
		{
			if (!text.Contains(SheetHeading, StringComparison.Ordinal))
			{
				continue;
			}

			var boxes = text.Split('\n')
				.Where(line => line.TrimStart().StartsWith("- [", StringComparison.Ordinal))
				.ToList();
			if (boxes.Count == 0)
			{
				violations.Add($"{path}: declares a run sheet but carries no checkbox step");
				continue;
			}

			var closed = path.StartsWith("docs/backlog/done/", StringComparison.Ordinal)
				|| text.Contains("- Status: Done", StringComparison.Ordinal);
			if (!closed)
			{
				continue;
			}

			var open = boxes.Count(line => line.TrimStart().StartsWith("- [ ]", StringComparison.Ordinal));
			if (open > 0)
			{
				violations.Add($"{path}: closed while {open} run-sheet step(s) are still open");
			}
		}

		Assert.True(violations.Count == 0,
			"run sheets disagree with their tickets' closure:" + Environment.NewLine + string.Join(Environment.NewLine, violations));
	}

	private static IEnumerable<(string Path, string Text)> ReadTickets()
	{
		var root = FindRepositoryRoot();
		foreach (var folder in new[] { "todo", "review", "done" })
		{
			var directory = Path.Combine(root, "docs", "backlog", folder);
			if (!Directory.Exists(directory))
			{
				continue;
			}

			foreach (var file in Directory.EnumerateFiles(directory, "*.md").OrderBy(f => f, StringComparer.Ordinal))
			{
				yield return ($"docs/backlog/{folder}/{Path.GetFileName(file)}", File.ReadAllText(file));
			}
		}
	}

	private static string FindRepositoryRoot()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory is not null)
		{
			if (Directory.Exists(Path.Combine(directory.FullName, "docs", "backlog")))
			{
				return directory.FullName;
			}

			directory = directory.Parent;
		}

		throw new InvalidOperationException("repository root (docs/backlog) not found above " + AppContext.BaseDirectory);
	}
}
