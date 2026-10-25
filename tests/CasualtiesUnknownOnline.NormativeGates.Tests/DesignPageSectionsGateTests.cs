using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// Every design page carries the sections its kind must have. A behaviour page (a mechanic, an item family, a surface)
/// carries nine; a rule page (the model, the cross-cutting rules) carries its own rule sections under whatever headings
/// the rules need, plus four fixed ones. The template is `docs/design/AGENTS.md`'s, and this gate is what makes "a section
/// that does not apply says why in one line" enforceable: without it a page can silently lose the section that would have
/// shown the defect. Index pages (`INDEX.md`, the area READMEs) are lists, not pages, and are exempt.
/// </summary>
public sealed class DesignPageSectionsGateTests
{
	private static readonly string[] BehaviourDirectories = ["mechanics", "items", "surfaces"];

	private static readonly string[] RuleDirectories = ["model", "cross-cutting"];

	/// <summary>The nine sections of a behaviour page, with the documented alternative names.</summary>
	private static readonly string[][] BehaviourSections =
	[
		["What the player does"],
		["What the game itself does", "What it is"],
		["Rules and states", "Parts and states"],
		["Refusals"],
		["Who judges"],
		["What travels"],
		["What the player sees and hears"],
		["Records"],
		["Gaps"],
	];

	/// <summary>A rule page's four fixed sections; its rule headings are the page's own.</summary>
	private static readonly string[][] RuleSections =
	[
		["Who judges"],
		["What travels"],
		["Records"],
		["Gaps"],
	];

	[Fact]
	public void EveryBehaviourPage_CarriesTheNineSections()
	{
		var failures = new List<string>();
		foreach (var page in Pages(BehaviourDirectories))
		{
			foreach (var section in Missing(File.ReadAllText(page), BehaviourSections))
			{
				failures.Add($"{Relative(page)}: no `## {section}` section");
			}
		}

		Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
	}

	[Fact]
	public void EveryRulePage_CarriesItsFourFixedSections()
	{
		var failures = new List<string>();
		foreach (var page in Pages(RuleDirectories))
		{
			foreach (var section in Missing(File.ReadAllText(page), RuleSections))
			{
				failures.Add($"{Relative(page)}: no `## {section}` section");
			}
		}

		Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
	}

	[Fact]
	public void TheMatcher_AcceptsACompletePage_AndRefusesOneMissingASection()
	{
		const string complete = "## What the player does\n## What it is\n## Parts and states\n## Refusals\n## Who judges\n" +
			"## What travels\n## What the player sees and hears\n## Records\n## Gaps";
		const string incomplete = "## What the player does\n## Records\n## Gaps";

		Assert.Empty(Missing(complete, BehaviourSections));
		Assert.NotEmpty(Missing(incomplete, BehaviourSections));
	}

	/// <summary>The first name of every section group the page does not carry under any of its names.</summary>
	private static List<string> Missing(string text, string[][] sections) =>
		[.. sections
			.Where(group => !group.Any(name => text.Contains("## " + name, StringComparison.Ordinal)))
			.Select(group => group[0])];

	private static IEnumerable<string> Pages(string[] directories) =>
		directories
			.Select(directory => Path.Combine(RepositoryPaths.Root, "docs", "design", directory))
			.Where(Directory.Exists)
			.SelectMany(directory => Directory.EnumerateFiles(directory, "*.md", SearchOption.AllDirectories))
			.Where(path => !string.Equals(Path.GetFileName(path), "README.md", StringComparison.OrdinalIgnoreCase))
			.OrderBy(path => path, StringComparer.Ordinal);

	private static string Relative(string path) =>
		Path.GetRelativePath(RepositoryPaths.Root, path).Replace('\\', '/');
}
