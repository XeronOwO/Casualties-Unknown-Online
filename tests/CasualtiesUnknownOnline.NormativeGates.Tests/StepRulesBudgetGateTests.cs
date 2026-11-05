using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// The step rules' own budget, made checkable. `docs/development/rules/README.md` states every committed step file stays
/// under 3 KB - "a file that explains itself is a file that gets skipped" - and nothing checked it. Measured 2026-11-05:
/// `00-work-mode.md` was 12,686 bytes (the one file printed EVERY round), `13-acceptance.md` 6,175 and `02-design.md`
/// 4,074, while the round that measured them read all fourteen files and still broke three of its rules.
///
/// <para>
/// The gate reads the committed files only (the `.local.md` siblings are machine notes and are not part of the rule
/// budget), checks that the set is still the fourteen steps 00-13, and pins the comparison with a synthetic pair so a
/// ceiling that stops biting fails here instead of passing every file.
/// </para>
/// </summary>
public sealed class StepRulesBudgetGateTests
{
	private const string RulesDirectory = "docs/development/rules";

	/// <summary>The README's "never above 3 KB", as bytes.</summary>
	internal const int StepRuleCeilingBytes = 3072;

	/// <summary>One committed step file: its name and the step number the name carries.</summary>
	private static readonly Regex StepFilePattern = new(@"^(?<step>\d{2})-[a-z0-9-]+\.md$", RegexOptions.Compiled);

	[Fact]
	public void EveryStepRuleFile_StaysInsideItsBudget()
	{
		var files = StepFiles();
		var oversized = Oversized(files.Select(file => (file.Name, file.Bytes)), StepRuleCeilingBytes);

		Assert.True(
			oversized.Count == 0,
			$"{oversized.Count} step rule file(s) are over the {StepRuleCeilingBytes}-byte budget the README states: "
			+ string.Join("; ", oversized)
			+ " - keep the checkable rules and move the measured history to why.md");
	}

	[Fact]
	public void TheStepSet_IsStillTheFourteenSteps()
	{
		var steps = StepFiles().Select(StepNumberOf).OrderBy(step => step).ToList();

		Assert.True(
			steps.Count >= 14,
			$"the walk found {steps.Count} step file(s) under {RulesDirectory}; it stopped seeing the set");
		Assert.True(
			steps.SequenceEqual(Enumerable.Range(0, 14)),
			"the step files are no longer exactly 00-13: a step was added, removed or renamed without the README and this gate");
	}

	[Fact]
	public void TheCeilingComparison_FlagsOnlyFilesAboveTheLimit()
	{
		var atLimit = Oversized([("10-structure.md", StepRuleCeilingBytes)], StepRuleCeilingBytes);
		var overLimit = Oversized([("99-new.md", StepRuleCeilingBytes + 1)], StepRuleCeilingBytes);

		Assert.Empty(atLimit);
		Assert.Contains("99-new.md", Assert.Single(overLimit), StringComparison.Ordinal);
	}

	[Fact]
	public void TheStepNumberReader_ReadsTheTwoDigitsAndNothingElse()
	{
		Assert.Equal(0, StepNumberOf(new StepFile("00-work-mode.md", 0)));
		Assert.Equal(13, StepNumberOf(new StepFile("13-acceptance.md", 0)));
	}

	private static int StepNumberOf(StepFile file) =>
		int.Parse(StepFilePattern.Match(file.Name).Groups["step"].Value);

	private static List<StepFile> StepFiles() =>
	[
		.. Directory.EnumerateFiles(RepositoryPaths.File(RulesDirectory), "*.md", SearchOption.TopDirectoryOnly)
			.Where(path => StepFilePattern.IsMatch(Path.GetFileName(path)))
			.Select(path => new StepFile(Path.GetFileName(path), (int)new FileInfo(path).Length))
			.OrderBy(file => file.Name, StringComparer.Ordinal),
	];

	internal static List<string> Oversized(IEnumerable<(string Name, int Bytes)> files, int ceiling) =>
	[
		.. files.Where(file => file.Bytes > ceiling).Select(file => $"{file.Name}: {file.Bytes} bytes"),
	];

	/// <summary>One committed step file and the bytes it carries.</summary>
	private sealed record StepFile(string Name, int Bytes);
}
