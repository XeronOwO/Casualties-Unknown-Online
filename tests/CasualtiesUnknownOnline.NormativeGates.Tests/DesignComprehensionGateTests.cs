using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// A ticket opened after the design corpus exists must name the corpus page it was designed from. The gate reads
/// every ticket in `docs/backlog/todo/` and `docs/backlog/review/`, skips the ones listed in
/// `docs/contracts/design-comprehension-baseline.txt` (the tickets written before the corpus), and requires the rest to
/// carry a `## Design` section that names at least one `docs/design/…` page. The point is not documentation for its own
/// sake: it is that work starts from a page that states the player's action, the rules and the authority, instead of
/// from whatever source file happened to be open.
/// </summary>
public sealed class DesignComprehensionGateTests
{
	private const string BaselineRelativePath = "docs/contracts/design-comprehension-baseline.txt";

	private static readonly string[] TicketDirectories = ["docs/backlog/todo", "docs/backlog/review"];

	[Fact]
	public void EveryTicketOpenedWithTheCorpus_NamesTheDesignPageItWasReadFrom()
	{
		var baseline = Baseline();
		var failures = new List<string>();
		foreach (var path in Tickets())
		{
			var relative = RepositoryRelative(path);
			if (baseline.Contains(relative))
			{
				continue;
			}

			var text = File.ReadAllText(path);
			if (!CarriesComprehension(text))
			{
				failures.Add($"{relative}: no `## Design` section naming a `docs/design/…` page");
			}
		}

		Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
	}

	[Fact]
	public void TheBaseline_NamesOnlyTicketsThatStillExist()
	{
		var tickets = Tickets().Select(RepositoryRelative).ToHashSet(StringComparer.Ordinal);
		var stale = Baseline().Where(entry => !tickets.Contains(entry)).OrderBy(entry => entry, StringComparer.Ordinal).ToList();

		Assert.True(stale.Count == 0, "the baseline names tickets that are gone; move the reference with the ticket: "
			+ string.Join(", ", stale));
	}

	[Fact]
	public void TheMatcher_AcceptsAComprehensionSection_AndRefusesATicketWithoutOne()
	{
		const string named = "## Design\n\nRead `docs/design/mechanics/item-use.md` and the surfaces row for the drag gesture.";
		const string unnamed = "## Design\n\nRead the relevant source files before changing them.";

		Assert.True(CarriesComprehension(named));
		Assert.False(CarriesComprehension(unnamed));
	}

	private static bool CarriesComprehension(string text) =>
		text.Contains("## Design", StringComparison.Ordinal)
		&& text.Contains("docs/design/", StringComparison.Ordinal);

	private static IEnumerable<string> Tickets() =>
		TicketDirectories
			.Select(directory => Path.Combine(RepositoryPaths.Root, directory))
			.Where(Directory.Exists)
			.SelectMany(directory => Directory.EnumerateFiles(directory, "*.md", SearchOption.TopDirectoryOnly))
			.OrderBy(path => path, StringComparer.Ordinal);

	private static HashSet<string> Baseline() =>
		File.ReadAllLines(Path.Combine(RepositoryPaths.Root, BaselineRelativePath))
			.Where(line => line.Trim().Length > 0 && !line.TrimStart().StartsWith('#'))
			.Select(line => line.Trim())
			.ToHashSet(StringComparer.Ordinal);

	private static string RepositoryRelative(string path) =>
		Path.GetRelativePath(RepositoryPaths.Root, path).Replace('\\', '/');
}
