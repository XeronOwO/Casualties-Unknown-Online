using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// Backlog cross-reference rot guard. Moving a ticket between status folders IS the
/// status transition, and the index row moves with it — but PROSE references
/// ("Related:", "tracked in", a gap row's ticket cell, a decision naming its owner
/// cycle) are hand-maintained and rot silently: the sentence keeps pointing at the
/// folder the ticket left, and nothing else checks it. Both shapes this repository
/// writes are covered — the short form <c>todo/ticket.md</c> and the full path
/// <c>docs/backlog/todo/ticket.md</c>. The status folders and the point-in-time
/// exemptions come from <see cref="BacklogIntegrityGateTests"/>'s canonical lists, so a
/// folder added there cannot silently escape this check, and a record that is allowed
/// to be stale there is allowed to be stale here.
/// </summary>
public class BacklogReferenceGateTests
{
	/// <summary>Guards the scan against silently checking nothing — the sibling gates carry the same kind of floor.</summary>
	private const int ReferenceFloor = 300;

	/// <summary>The status folders, from the one canonical list.</summary>
	private static readonly string[] StatusFolders = [.. BacklogIntegrityGateTests.StatusOfFolder.Keys];

	/// <summary>Both reference shapes. The status alternation is derived, never copied; the leading guard keeps a match from starting inside a longer path fragment.</summary>
	private static readonly Regex TicketReference = new(
		$@"(?<![\w./-])(?:docs/backlog/)?(?:{string.Join("|", StatusFolders.Select(Regex.Escape))})/[A-Za-z0-9_\-]+\.md");

	/// <summary>The matcher's own contract, so a later edit to the pattern cannot quietly narrow the scan to nothing.</summary>
	[Fact]
	public void TheMatcher_SeesBothReferenceShapesAndIgnoresTheirLookalikes()
	{
		Assert.Matches(TicketReference, "the split is recorded in `todo/guest-break-drops-recovery.md`");
		Assert.Matches(TicketReference, "Owner cycle: backlog `docs/backlog/in-progress/global-adaptive-report-rate-stage-2-traffic-bandwidth.md`");
		Assert.Matches(TicketReference, "[the ticket](review/block-break-first-writer-wins.md)");
		Assert.Matches(TicketReference, "moved to `docs/backlog/review/trap-action-divergence-hardening.md` today");

		Assert.DoesNotMatch(TicketReference, "the tickets live under docs/backlog/ and are indexed in README.md");
		Assert.DoesNotMatch(TicketReference, "a mid-word fragment btodo/x.md is not a reference");
		Assert.DoesNotMatch(TicketReference, "see docs/format.md for the document rules");
		Assert.DoesNotMatch(TicketReference, "src/CasualtiesUnknownOnline.Runtime/Session/World/WorldService.cs");
	}

	[Fact]
	public void EveryBacklogReference_ResolvesToATicket()
	{
		var docsRoot = RepositoryPaths.File("docs");
		var references = 0;
		var failures = new List<string>();
		foreach (var file in GatedDocuments(docsRoot))
		{
			var relative = Path.GetRelativePath(RepositoryPaths.Root, file);
			foreach (Match match in TicketReference.Matches(File.ReadAllText(file)))
			{
				references++;
				var ticket = match.Value.StartsWith("docs/backlog/", StringComparison.Ordinal)
					? match.Value["docs/backlog/".Length..]
					: match.Value;
				if (!File.Exists(Path.Combine(docsRoot, "backlog", ticket)))
				{
					failures.Add($"{relative}: {match.Value}");
				}
			}
		}

		Assert.True(
			references >= ReferenceFloor,
			$"the scan read only {references} backlog reference(s); the matcher or the document set shrank and this rule would pass by checking almost nothing");
		Assert.True(
			failures.Count == 0,
			"backlog references that do not resolve to a ticket (move the reference with the ticket):"
				+ Environment.NewLine
				+ string.Join(Environment.NewLine, failures.Distinct().OrderBy(failure => failure, StringComparer.Ordinal)));
	}

	/// <summary>Every markdown document under <c>docs/</c> except the point-in-time records the anchor gate also exempts.</summary>
	private static IEnumerable<string> GatedDocuments(string docsRoot) =>
		Directory.EnumerateFiles(docsRoot, "*.md", SearchOption.AllDirectories)
			.Where(path =>
			{
				var relative = Path.GetRelativePath(docsRoot, path).Replace('\\', '/');
				return !BacklogIntegrityGateTests.RecordPrefixes.Any(prefix => relative.StartsWith(prefix, StringComparison.Ordinal));
			});
}
