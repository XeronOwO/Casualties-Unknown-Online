using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// The bilingual human-guide pairing gate: the two guide levels are maintained as English +
/// Chinese + consistency record, and a pair that drifts apart fails here. The policy is
/// `docs/i18n/README.md`; the checks themselves are pinned by the synthetic contract cases below.
/// </summary>
public class HumanDocsPairingGateTests
{
	private const int WorkspaceInstructionBudgetBytes = 65536;
	private const int NestedInstructionCeilingBytes = 5120;

	private const string SampleEnglish = "# Title\n\nEnglish | [中文](sample.zh.md)\n\nIntro paragraph.\n\n## Section\n\n- one\n- two\n";
	private const string SampleChinese = "# 标题\n\n[English](sample.md) | 中文\n\n简介段落。\n\n## 小节\n\n- 一\n- 二\n";
	private const string SamplePath = "docs/guide/sample.md";

	[Fact]
	public void EveryPairedTopic_HasItsThreeSiblingFiles()
	{
		var topics = HumanDocsPairing.DiscoverEnglishSources(RepositoryPaths.Root);
		Assert.True(
			topics.Count >= HumanDocsPairing.MinimumPairedTopics,
			$"discovery found {topics.Count} paired topics; the floor is {HumanDocsPairing.MinimumPairedTopics} — the scope roots or the discovery filter stopped seeing the guide tree");

		var failures = new List<string>();
		foreach (var topic in topics)
		{
			foreach (var sibling in new[] { HumanDocsPairing.CounterpartPath(topic), HumanDocsPairing.RecordPath(topic) })
			{
				if (!File.Exists(RepositoryPaths.File(sibling)))
				{
					failures.Add($"{topic}: missing sibling {sibling}");
				}
			}
		}

		Assert.True(failures.Count == 0, Describe("pair completeness", failures));
	}

	[Fact]
	public void EveryPair_MatchesItsRecordedBlobHashes()
	{
		var failures = new List<string>();

		foreach (var topic in HumanDocsPairing.DiscoverEnglishSources(RepositoryPaths.Root))
		{
			var chinese = HumanDocsPairing.CounterpartPath(topic);
			var record = HumanDocsPairing.RecordPath(topic);
			if (!File.Exists(RepositoryPaths.File(chinese)) || !File.Exists(RepositoryPaths.File(record)))
			{
				// A missing sibling is this pair's finding in EveryPairedTopic_HasItsThreeSiblingFiles;
				// this case owns whether the contents still match the record.
				continue;
			}

			var recordText = File.ReadAllText(RepositoryPaths.File(record));
			var recordFailures = HumanDocsPairing.RecordFailures(topic, recordText);
			if (recordFailures.Count > 0)
			{
				failures.AddRange(recordFailures);
				continue;
			}

			var recorded = HumanDocsPairing.RecordEntries(recordText);
			var hashes = GitBlobHashes(topic, chinese);
			if (hashes.Count != 2)
			{
				failures.Add($"{topic}: git hash-object returned {hashes.Count} hashes for two paths");
				continue;
			}

			foreach (var (path, hash) in new[] { (topic, hashes[0]), (chinese, hashes[1]) })
			{
				var name = Path.GetFileName(path);
				if (!string.Equals(recorded[name], hash, StringComparison.Ordinal))
				{
					failures.Add($"{path}: recorded {recorded[name]} but the file hashes to {hash} — one side was edited without re-recording the pair");
				}
			}
		}

		Assert.True(failures.Count == 0, Describe("pair consistency record", failures));
	}

	[Fact]
	public void EveryPair_CarriesBothLanguageSwitchers()
	{
		var failures = new List<string>();

		foreach (var topic in HumanDocsPairing.DiscoverEnglishSources(RepositoryPaths.Root))
		{
			var chinese = HumanDocsPairing.CounterpartPath(topic);
			if (!File.Exists(RepositoryPaths.File(chinese)))
			{
				continue;
			}

			failures.AddRange(HumanDocsPairing.SwitcherFailures(topic, File.ReadAllText(RepositoryPaths.File(topic)), File.ReadAllText(RepositoryPaths.File(chinese))));
		}

		Assert.True(failures.Count == 0, Describe("language switcher", failures));
	}

	[Fact]
	public void EveryPair_MirrorsItsStructureAndLinks()
	{
		var failures = new List<string>();

		foreach (var topic in HumanDocsPairing.DiscoverEnglishSources(RepositoryPaths.Root))
		{
			var chinese = HumanDocsPairing.CounterpartPath(topic);
			if (!File.Exists(RepositoryPaths.File(chinese)))
			{
				continue;
			}

			failures.AddRange(HumanDocsPairing.StructureFailures(topic, File.ReadAllText(RepositoryPaths.File(topic)), File.ReadAllText(RepositoryPaths.File(chinese))));
		}

		Assert.True(failures.Count == 0, Describe("structural mirror", failures));
	}

	[Fact]
	public void NoPairedArtifact_LivesOutsideTheGuideScope()
	{
		var strays = HumanDocsPairing.PairedArtifactsOutsideScope(RepositoryFiles());
		Assert.True(
			strays.Count == 0,
			$"only {HumanDocsPairing.GuideRoot}/ and {HumanDocsPairing.DeveloperRoot}/ are paired; these files must be removed or moved into scope:{Environment.NewLine}{string.Join(Environment.NewLine, strays)}");

		// Synthetic contract for the inverse scan: a stray artifact is caught anywhere in the tree, and a
		// legitimate paired page is not.
		var sample = new List<string>
		{
			"docs/guide/playing.md",
			"docs/guide/playing.zh.md",
			"docs/guide/playing.i18n.yaml",
			"src/CasualtiesUnknownOnline.Runtime/Notes.zh.md",
			"README.zh.md"
		};
		Assert.Equal(2, HumanDocsPairing.PairedArtifactsOutsideScope(sample).Count);
	}

	[Fact]
	public void AgentInstructionChain_StaysInsideTheWorkspaceBudget()
	{
		var nested = RepositoryPaths.File("docs/AGENTS.md");
		Assert.True(File.Exists(nested), "docs/AGENTS.md is the docs-subtree instruction file and is missing");
		var nestedSize = new FileInfo(nested).Length;
		Assert.True(
			nestedSize <= NestedInstructionCeilingBytes,
			$"docs/AGENTS.md is {nestedSize} bytes; a nested instruction file must stay at or under {NestedInstructionCeilingBytes} bytes so the root instruction file keeps its place in the loader's budget");

		var chain = new List<string> { "AGENTS.md", "docs/AGENTS.md" };
		if (File.Exists(RepositoryPaths.File("AGENTS.local.md")))
		{
			// The machine-local file is untracked, but it competes for the SAME loader budget as the
			// tracked files, so excluding it would let the gate report headroom that does not exist.
			chain.Add("AGENTS.local.md");
		}

		var sizes = chain.ToDictionary(relative => relative, relative => new FileInfo(RepositoryPaths.File(relative)).Length);
		var total = sizes.Values.Sum();
		Assert.True(
			total <= WorkspaceInstructionBudgetBytes,
			$"the workspace instruction chain is {total} bytes, over the {WorkspaceInstructionBudgetBytes}-byte budget: {string.Join(", ", sizes.Select(entry => $"{entry.Key} {entry.Value}"))} — the loader drops whichever instruction file no longer fits");
	}

	[Fact]
	public void ThePairingChecks_AcceptAValidPair()
	{
		Assert.Empty(HumanDocsPairing.SwitcherFailures(SamplePath, SampleEnglish, SampleChinese));
		Assert.Empty(HumanDocsPairing.StructureFailures(SamplePath, SampleEnglish, SampleChinese));
		Assert.Empty(HumanDocsPairing.RecordFailures(SamplePath, "sample.md: " + new string('a', 40) + "\nsample.zh.md: " + new string('b', 40) + "\n"));
	}

	[Fact]
	public void ThePairingChecks_FlagEveryContractBreak()
	{
		// A missing switcher: the line after the H1 is then ordinary content.
		Assert.NotEmpty(HumanDocsPairing.SwitcherFailures(SamplePath, SampleEnglish, SampleChinese.Replace("[English](sample.md) | 中文\n", string.Empty, StringComparison.Ordinal)));

		// A wrong switcher target.
		Assert.NotEmpty(HumanDocsPairing.SwitcherFailures(SamplePath, SampleEnglish.Replace("(sample.zh.md)", "(other.zh.md)", StringComparison.Ordinal), SampleChinese));

		// One extra list item.
		Assert.NotEmpty(HumanDocsPairing.StructureFailures(SamplePath, SampleEnglish, SampleChinese + "- 三\n"));

		// A dropped paragraph.
		Assert.NotEmpty(HumanDocsPairing.StructureFailures(SamplePath, SampleEnglish, SampleChinese.Replace("简介段落。\n\n", string.Empty, StringComparison.Ordinal)));

		// A different table width.
		const string englishTable = "# Title\n\nEnglish | [中文](sample.zh.md)\n\nIntro.\n\n| a | b |\n|---|---|\n| 1 | 2 |\n";
		const string chineseTable = "# 标题\n\n[English](sample.md) | 中文\n\n简介。\n\n| 甲 | 乙 | 丙 |\n|---|---|---|\n| 一 | 二 | 三 |\n";
		Assert.NotEmpty(HumanDocsPairing.StructureFailures(SamplePath, englishTable, chineseTable));

		// A code block whose body is not identical.
		const string englishFence = "# Title\n\nEnglish | [中文](sample.zh.md)\n\nIntro.\n\n```text\ndotnet test A\n```\n";
		const string chineseFence = "# 标题\n\n[English](sample.md) | 中文\n\n简介。\n\n```text\ndotnet test B\n```\n";
		Assert.NotEmpty(HumanDocsPairing.StructureFailures(SamplePath, englishFence, chineseFence));

		// A link whose target was not localized.
		const string englishLink = "# Title\n\nEnglish | [中文](sample.zh.md)\n\nSee [the guide](playing.md).\n";
		const string chineseLink = "# 标题\n\n[English](sample.md) | 中文\n\n见[指南](other.md)。\n";
		Assert.NotEmpty(HumanDocsPairing.StructureFailures(SamplePath, englishLink, chineseLink));

		// A record that misses a side, names a stranger, or carries a short hash.
		Assert.NotEmpty(HumanDocsPairing.RecordFailures(SamplePath, "sample.md: " + new string('a', 40) + "\n"));
		Assert.NotEmpty(HumanDocsPairing.RecordFailures(SamplePath, "sample.md: " + new string('a', 40) + "\nsample.zh.md: " + new string('b', 40) + "\nstray.md: " + new string('c', 40) + "\n"));
		Assert.NotEmpty(HumanDocsPairing.RecordFailures(SamplePath, "sample.md: abc\nsample.zh.md: " + new string('b', 40) + "\n"));

		// A link carried by one side's list item but not the other's: the item shape differs.
		const string englishShaped = "# Title\n\nEnglish | [中文](sample.zh.md)\n\nIntro.\n\n- see [the guide](playing.md)\n- plain item\n";
		const string chineseShaped = "# 标题\n\n[English](sample.md) | 中文\n\n简介。\n\n- 见[指南](playing.zh.md)\n- 普通条目\n";
		Assert.Empty(HumanDocsPairing.StructureFailures(SamplePath, englishShaped, chineseShaped));
		Assert.NotEmpty(HumanDocsPairing.StructureFailures(SamplePath, englishShaped, chineseShaped.Replace("见[指南](playing.zh.md)", "见指南", StringComparison.Ordinal)));

		// A table cell that carries inline code on one side only: the cell shape differs, though the column
		// counts agree.
		const string englishCells = "# Title\n\nEnglish | [中文](sample.zh.md)\n\nIntro.\n\n| key | value |\n|---|---|\n| `F6` | opens the panel |\n";
		const string chineseCells = "# 标题\n\n[English](sample.md) | 中文\n\n简介。\n\n| 按键 | 作用 |\n|---|---|\n| `F6` | 打开面板 |\n";
		Assert.Empty(HumanDocsPairing.StructureFailures(SamplePath, englishCells, chineseCells));
		Assert.NotEmpty(HumanDocsPairing.StructureFailures(SamplePath, englishCells, chineseCells.Replace("| `F6` |", "| F6 |", StringComparison.Ordinal)));

		// An escaped pipe inside a cell is content, not a separator: both sides still have two columns.
		const string englishEscaped = "# Title\n\nEnglish | [中文](sample.zh.md)\n\nIntro.\n\n| a | b |\n|---|---|\n| left \\| right | second |\n";
		const string chineseEscaped = "# 标题\n\n[English](sample.md) | 中文\n\n简介。\n\n| 甲 | 乙 |\n|---|---|\n| 左 \\| 右 | 第二列 |\n";
		Assert.Empty(HumanDocsPairing.StructureFailures(SamplePath, englishEscaped, chineseEscaped));

		// A reference-style link definition that points at a different file.
		const string englishReference = "# Title\n\nEnglish | [中文](sample.zh.md)\n\nIntro.[^1]\n\n[^1]: playing.md\n";
		const string chineseReference = "# 标题\n\n[English](sample.md) | 中文\n\n简介。[^1]\n\n[^1]: other.md\n";
		Assert.NotEmpty(HumanDocsPairing.StructureFailures(SamplePath, englishReference, chineseReference));

		// A dropped middle block: the report must state the length difference, not only one mislocated pair.
		const string droppedMiddle = "# 标题\n\n[English](sample.md) | 中文\n\n简介段落。\n\n- 一\n- 二\n";
		var dropped = HumanDocsPairing.StructureFailures(SamplePath, SampleEnglish, droppedMiddle);
		Assert.Contains(dropped, failure => failure.Contains("blocks in English vs", StringComparison.Ordinal));
	}

	[Fact]
	public void TheStructureCheck_IgnoresLineWrappingAndSwitcherWording()
	{
		// The same pair, with the English paragraph wrapped over two lines: wrapping is not structure.
		const string wrapped = "# Title\n\nEnglish | [中文](sample.zh.md)\n\nIntro\nparagraph.\n\n## Section\n\n- one\n- two\n";
		Assert.Empty(HumanDocsPairing.StructureFailures(SamplePath, wrapped, SampleChinese));

		// A wrapped list item stays one item.
		const string wrappedItem = "# Title\n\nEnglish | [中文](sample.zh.md)\n\nIntro paragraph.\n\n## Section\n\n- one\n  continued\n- two\n";
		Assert.Empty(HumanDocsPairing.StructureFailures(SamplePath, wrappedItem, SampleChinese));

		// The switcher line itself is excluded from the signature and the link list.
		Assert.DoesNotContain("sample.md", HumanDocsPairing.LinkTargets(SampleEnglish));
	}

	/// <summary>The repository's own file set: tracked files plus untracked-and-not-ignored ones.</summary>
	private static IReadOnlyList<string> RepositoryFiles() => GitLines("ls-files --cached --others --exclude-standard");

	private static IReadOnlyList<string> GitBlobHashes(params string[] repoRelativePaths) =>
		GitLines($"hash-object {string.Join(" ", repoRelativePaths.Select(path => "\"" + path + "\""))}");

	private static IReadOnlyList<string> GitLines(string arguments)
	{
		var startInfo = new ProcessStartInfo
		{
			FileName = "git",
			Arguments = $"-C \"{RepositoryPaths.Root}\" {arguments}",
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true
		};

		using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"could not start git {arguments}");
		var output = process.StandardOutput.ReadToEnd();
		var error = process.StandardError.ReadToEnd();
		process.WaitForExit();
		Assert.True(process.ExitCode == 0, $"git {arguments} failed{Environment.NewLine}{error}");
		return [.. output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(line => line.Trim())];
	}

	private static string Describe(string check, IReadOnlyList<string> failures) =>
		$"{check}: {failures.Count} violation(s){Environment.NewLine}{string.Join(Environment.NewLine, failures)}";
}
