using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// Pure logic behind the bilingual human-guide pairing gate: which documents are paired, how a
/// pair's language switchers, structural signature, link targets and consistency record are read.
/// The gate tests drive these functions against the real tree and against synthetic pairs, so the
/// checks themselves are pinned instead of only their verdict on today's files.
/// </summary>
internal static class HumanDocsPairing
{
	internal const string GuideRoot = "docs/guide";
	internal const string DeveloperRoot = "docs/developer";
	internal const string ChineseSuffix = ".zh.md";
	internal const string RecordSuffix = ".i18n.yaml";

	/// <summary>
	/// Discovery floor: the paired corpus holds more topics than this (6 player pages, 9 developer
	/// pages and 2 guide indexes today), so the floor only catches a discovery break — a renamed
	/// root or a lost scope root — rather than pinning the count, which would have to be edited
	/// every time a page is added.
	/// </summary>
	internal const int MinimumPairedTopics = 12;

	private static readonly string[] ScopeRoots = [GuideRoot, DeveloperRoot];

	private static readonly Regex LinkPattern = new(@"!?\[[^\]]*\]\(([^)\s]+)(?:\s+""[^""]*"")?\)");
	private static readonly Regex HeadingPattern = new(@"^#{1,6}\s+\S");
	private static readonly Regex UnorderedItemPattern = new(@"^(\s*)[-*+]\s+(.*)$");
	private static readonly Regex OrderedItemPattern = new(@"^(\s*)(\d+)[.)]\s+(.*)$");
	private static readonly Regex RulePattern = new(@"^(-{3,}|\*{3,}|_{3,})$");
	private static readonly Regex RecordLinePattern = new(@"^([^\s:]+):\s*([0-9a-f]{40})$");
	private static readonly Regex ReferenceLinkPattern = new(@"^\s{0,3}\[([^\]]+)\]:\s*(\S+)");

	internal static IReadOnlyList<string> ScopeRootsInOrder() => ScopeRoots;

	internal static string ToRepoRelative(string repoRoot, string fullPath) =>
		Path.GetRelativePath(repoRoot, fullPath).Replace('\\', '/');

	internal static bool IsUnderScope(string repoRelativePath) =>
		ScopeRoots.Any(root => repoRelativePath.StartsWith(root + "/", StringComparison.Ordinal));

	internal static bool IsEnglishSource(string repoRelativePath) =>
		repoRelativePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
		&& !repoRelativePath.EndsWith(ChineseSuffix, StringComparison.OrdinalIgnoreCase);

	internal static bool IsPairedArtifact(string repoRelativePath) =>
		repoRelativePath.EndsWith(ChineseSuffix, StringComparison.OrdinalIgnoreCase)
		|| repoRelativePath.EndsWith(RecordSuffix, StringComparison.OrdinalIgnoreCase);

	internal static string CounterpartPath(string englishPath) => englishPath[..^3] + ChineseSuffix;

	internal static string RecordPath(string englishPath) => englishPath[..^3] + RecordSuffix;

	internal static IReadOnlyList<string> DiscoverEnglishSources(string repoRoot) =>
		[.. ScopeRoots
			.SelectMany(root => Directory.EnumerateFiles(Path.Combine(repoRoot, root), "*.md", SearchOption.AllDirectories))
			.Select(path => ToRepoRelative(repoRoot, path))
			.Where(IsEnglishSource)
			.OrderBy(path => path, StringComparer.Ordinal)];

	/// <summary>
	/// Every <c>.zh.md</c> or <c>.i18n.yaml</c> in the repository that is not a paired guide page. The
	/// candidate list is the repository's own file set (tracked plus untracked-but-not-ignored), so the
	/// inverse rule covers the whole tree rather than only the directory the paired pages live in.
	/// </summary>
	internal static IReadOnlyList<string> PairedArtifactsOutsideScope(IReadOnlyList<string> repositoryFiles) =>
		[.. repositoryFiles
			.Where(IsPairedArtifact)
			.Where(path => !IsUnderScope(path))
			.OrderBy(path => path, StringComparer.Ordinal)];

	internal static string ExpectedEnglishSwitcher(string englishPath) =>
		$"English | [中文]({Path.GetFileName(CounterpartPath(englishPath))})";

	internal static string ExpectedChineseSwitcher(string englishPath) =>
		$"[English]({Path.GetFileName(englishPath)}) | 中文";

	/// <summary>The line immediately after the H1, which carries the language switcher.</summary>
	internal static string? ReadSwitcher(string markdown)
	{
		var lines = SplitLines(markdown);
		var index = SwitcherLineIndex(lines);
		return index < 0 ? null : lines[index].Trim();
	}

	internal static IReadOnlyList<string> SwitcherFailures(string englishPath, string englishText, string chineseText)
	{
		var failures = new List<string>();

		var expectedEnglish = ExpectedEnglishSwitcher(englishPath);
		var english = ReadSwitcher(englishText);
		if (english is null)
		{
			failures.Add($"{englishPath}: no H1 followed by a switcher line");
		}
		else if (!string.Equals(english, expectedEnglish, StringComparison.Ordinal))
		{
			failures.Add($"{englishPath}: switcher is '{english}', expected '{expectedEnglish}'");
		}

		var chinesePath = CounterpartPath(englishPath);
		var expectedChinese = ExpectedChineseSwitcher(englishPath);
		var chinese = ReadSwitcher(chineseText);
		if (chinese is null)
		{
			failures.Add($"{chinesePath}: no H1 followed by a switcher line");
		}
		else if (!string.Equals(chinese, expectedChinese, StringComparison.Ordinal))
		{
			failures.Add($"{chinesePath}: switcher is '{chinese}', expected '{expectedChinese}'");
		}

		return failures;
	}

	/// <summary>
	/// The block sequence both sides must share: headings by depth, paragraphs as units (line wrapping is
	/// not structure), list items by kind, depth and shape, table rows by cell count and cell shape,
	/// quotes, rules, and code blocks including their verbatim body. Translated words are never compared —
	/// the shape markers are language-independent counts (links, inline code, bold spans) — so reordering
	/// two items that carry the same shape is invisible here, which the policy page states as a limit.
	/// </summary>
	internal static IReadOnlyList<string> Signature(string markdown)
	{
		var lines = SplitLines(markdown);
		var switcherIndex = SwitcherLineIndex(lines);
		var tokens = new List<string>();
		var fence = new StringBuilder();
		var fenceInfo = string.Empty;
		var inFence = false;
		var inParagraph = false;
		var inList = false;

		for (var i = 0; i < lines.Length; i++)
		{
			if (i == switcherIndex)
			{
				continue;
			}

			var line = lines[i].TrimEnd();

			if (inFence)
			{
				if (IsFenceLine(line.TrimStart()))
				{
					tokens.Add($"fence[{fenceInfo}]{fence}");
					inFence = false;
				}
				else
				{
					fence.Append(line).Append('\n');
				}

				continue;
			}

			if (line.Trim().Length == 0)
			{
				inParagraph = false;
				inList = false;
				continue;
			}

			if (IsFenceLine(line.TrimStart()))
			{
				inFence = true;
				fenceInfo = line.Trim().Trim('`', '~');
				fence.Clear();
				inParagraph = false;
				inList = false;
				continue;
			}

			if (HeadingPattern.IsMatch(line))
			{
				tokens.Add($"h{line.TakeWhile(character => character == '#').Count()}");
				inParagraph = false;
				inList = false;
				continue;
			}

			if (line.TrimStart().StartsWith("|", StringComparison.Ordinal))
			{
				tokens.Add($"table{CellShapes(line)}");
				inParagraph = false;
				inList = false;
				continue;
			}

			if (UnorderedItemPattern.IsMatch(line) || OrderedItemPattern.IsMatch(line))
			{
				tokens.Add($"list{Indent(line)}{ListItemKind(line)}{ItemShape(line)}");
				inParagraph = false;
				inList = true;
				continue;
			}

			if (line.TrimStart().StartsWith(">", StringComparison.Ordinal))
			{
				tokens.Add("quote");
				inParagraph = false;
				inList = false;
				continue;
			}

			if (RulePattern.IsMatch(line.Trim()))
			{
				tokens.Add("rule");
				inParagraph = false;
				inList = false;
				continue;
			}

			if (inList)
			{
				// Lazy continuation of a list item: how a line is wrapped is not structure.
				continue;
			}

			if (!inParagraph)
			{
				tokens.Add("para");
				inParagraph = true;
			}
		}

		if (inFence)
		{
			tokens.Add($"fence[{fenceInfo}]{fence}");
		}

		return tokens;
	}

	/// <summary>
	/// Inline link and image targets plus reference-style definitions, in document order, normalized so a
	/// paired corpus target reads the same on both sides.
	/// </summary>
	internal static IReadOnlyList<string> LinkTargets(string markdown)
	{
		var lines = SplitLines(markdown);
		var switcherIndex = SwitcherLineIndex(lines);
		var targets = new List<string>();
		var inFence = false;

		for (var i = 0; i < lines.Length; i++)
		{
			if (i == switcherIndex)
			{
				continue;
			}

			var line = lines[i].TrimEnd();
			if (IsFenceLine(line.TrimStart()))
			{
				inFence = !inFence;
				continue;
			}

			if (inFence)
			{
				continue;
			}

			var definition = ReferenceLinkPattern.Match(line);
			if (definition.Success)
			{
				targets.Add(NormalizeLocale(definition.Groups[2].Value));
			}

			foreach (Match match in LinkPattern.Matches(line))
			{
				targets.Add(NormalizeLocale(match.Groups[1].Value));
			}
		}

		return targets;
	}

	internal static IReadOnlyList<string> StructureFailures(string englishPath, string englishText, string chineseText)
	{
		var pair = $"{englishPath} vs {CounterpartPath(englishPath)}";
		var failures = new List<string>();
		failures.AddRange(SequenceFailures(pair, "block", Signature(englishText), Signature(chineseText)));
		failures.AddRange(SequenceFailures(pair, "link", LinkTargets(englishText), LinkTargets(chineseText)));
		return failures;
	}

	/// <summary>
	/// Compares two token sequences around their common head and tail, so an inserted or dropped block is
	/// reported as a window difference plus a length difference instead of one mislocated pair.
	/// </summary>
	private static IReadOnlyList<string> SequenceFailures(string pair, string kind, IReadOnlyList<string> english, IReadOnlyList<string> chinese)
	{
		var failures = new List<string>();
		var head = 0;
		while (head < english.Count && head < chinese.Count && string.Equals(english[head], chinese[head], StringComparison.Ordinal))
		{
			head++;
		}

		var tail = 0;
		while (tail < english.Count - head && tail < chinese.Count - head
			&& string.Equals(english[english.Count - 1 - tail], chinese[chinese.Count - 1 - tail], StringComparison.Ordinal))
		{
			tail++;
		}

		if (head == english.Count && head == chinese.Count)
		{
			return failures;
		}

		if (english.Count != chinese.Count)
		{
			failures.Add($"{pair}: {english.Count} {kind}s in English vs {chinese.Count} in Chinese (first difference at {kind} {head + 1})");
		}

		var englishWindow = english.Skip(head).Take(english.Count - head - tail).ToList();
		var chineseWindow = chinese.Skip(head).Take(chinese.Count - head - tail).ToList();
		for (var i = 0; i < englishWindow.Count && i < chineseWindow.Count && failures.Count < 4; i++)
		{
			if (!string.Equals(englishWindow[i], chineseWindow[i], StringComparison.Ordinal))
			{
				failures.Add($"{pair}: {kind} {head + i + 1} differs — English '{Truncate(englishWindow[i])}' vs Chinese '{Truncate(chineseWindow[i])}'");
			}
		}

		if (failures.Count == 0)
		{
			failures.Add($"{pair}: the {kind} sequences differ only in their middle ({english.Count} English vs {chinese.Count} Chinese)");
		}

		return failures;
	}

	/// <summary>The blob hashes a record carries, keyed by sibling file name.</summary>
	internal static IReadOnlyDictionary<string, string> RecordEntries(string recordText)
	{
		var entries = new Dictionary<string, string>(StringComparer.Ordinal);
		foreach (var line in SplitLines(recordText))
		{
			var match = RecordLinePattern.Match(line.Trim());
			if (match.Success)
			{
				entries[match.Groups[1].Value] = match.Groups[2].Value;
			}
		}

		return entries;
	}

	/// <summary>The record names each side by its sibling file name and carries that side's blob hash.</summary>
	internal static IReadOnlyList<string> RecordFailures(string englishPath, string recordText)
	{
		var failures = new List<string>();
		var expected = new List<string> { Path.GetFileName(englishPath), Path.GetFileName(CounterpartPath(englishPath)) };
		var seen = new Dictionary<string, string>(StringComparer.Ordinal);
		var lines = SplitLines(recordText);

		for (var i = 0; i < lines.Length; i++)
		{
			var line = lines[i].Trim();
			if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
			{
				continue;
			}

			var match = RecordLinePattern.Match(line);
			if (!match.Success)
			{
				failures.Add($"{RecordPath(englishPath)}: line {i + 1} is not '<file>: <40-hex blob hash>'");
				continue;
			}

			seen[match.Groups[1].Value] = match.Groups[2].Value;
		}

		foreach (var name in expected.Where(name => !seen.ContainsKey(name)))
		{
			failures.Add($"{RecordPath(englishPath)}: does not name '{name}'");
		}

		foreach (var name in seen.Keys.Where(name => !expected.Contains(name, StringComparer.Ordinal)))
		{
			failures.Add($"{RecordPath(englishPath)}: names unexpected file '{name}'");
		}

		return failures;
	}

	private static int SwitcherLineIndex(string[] lines)
	{
		var heading = Array.FindIndex(lines, line => line.StartsWith("# ", StringComparison.Ordinal));
		if (heading < 0)
		{
			return -1;
		}

		for (var i = heading + 1; i < lines.Length; i++)
		{
			if (lines[i].Trim().Length > 0)
			{
				return i;
			}
		}

		return -1;
	}

	private static string[] SplitLines(string text) => text.Replace("\r\n", "\n").Split('\n');

	private static bool IsFenceLine(string trimmedStart) =>
		trimmedStart.StartsWith("```", StringComparison.Ordinal) || trimmedStart.StartsWith("~~~", StringComparison.Ordinal);

	/// <summary>
	/// A table row's language-independent shape: one marker group per cell, built from what the cell
	/// carries (a link, inline code, an escaped pipe) rather than from the translated words in it.
	/// </summary>
	private static string CellShapes(string line) => string.Join(",", SplitCells(line).Select(CellShape));

	private static string CellShape(string cell)
	{
		var shape = new StringBuilder();
		if (LinkPattern.IsMatch(cell))
		{
			shape.Append('l');
		}

		if (cell.Contains('`', StringComparison.Ordinal))
		{
			shape.Append('c');
		}

		if (cell.Contains('|', StringComparison.Ordinal))
		{
			shape.Append('p');
		}

		return shape.Length == 0 ? "-" : shape.ToString();
	}

	/// <summary>
	/// A list item's language-independent shape: how many links, inline code spans and bold spans the item
	/// carries. Counted, never compared word by word — see the gate's stated limit.
	/// </summary>
	private static string ItemShape(string line)
	{
		var ordered = OrderedItemPattern.Match(line);
		var content = ordered.Success ? ordered.Groups[3].Value : UnorderedItemPattern.Match(line).Groups[2].Value;
		var shape = new StringBuilder();

		var links = LinkPattern.Matches(content).Count;
		if (links > 0)
		{
			shape.Append('l').Append(links);
		}

		var code = content.Count(character => character == '`') / 2;
		if (code > 0)
		{
			shape.Append('c').Append(code);
		}

		var bold = content.Split("**", StringSplitOptions.None).Length / 2;
		if (bold > 0)
		{
			shape.Append('b').Append(bold);
		}

		return shape.Length == 0 ? "-" : shape.ToString();
	}

	/// <summary>
	/// Cells in a table row. A pipe escaped as <c>\|</c> or written inside an inline code span is cell
	/// content, not a separator — counting it as a separator reported a column difference where the table
	/// had none.
	/// </summary>
	private static IReadOnlyList<string> SplitCells(string line)
	{
		var cells = new List<string>();
		var current = new StringBuilder();
		var trimmed = line.Trim();
		var inCode = false;

		for (var i = 0; i < trimmed.Length; i++)
		{
			var character = trimmed[i];
			if (character == '\\' && i + 1 < trimmed.Length && trimmed[i + 1] == '|')
			{
				current.Append('|');
				i++;
				continue;
			}

			if (character == '`')
			{
				inCode = !inCode;
				current.Append(character);
				continue;
			}

			if (character == '|' && !inCode)
			{
				cells.Add(current.ToString().Trim());
				current.Clear();
				continue;
			}

			current.Append(character);
		}

		cells.Add(current.ToString().Trim());
		if (cells.Count > 0 && cells[0].Length == 0)
		{
			cells.RemoveAt(0);
		}

		if (cells.Count > 0 && cells[cells.Count - 1].Length == 0)
		{
			cells.RemoveAt(cells.Count - 1);
		}

		return cells;
	}

	private static string NormalizeLocale(string target) => target.Replace(ChineseSuffix, ".md", StringComparison.Ordinal);

	private static int Indent(string line) => line.Length - line.TrimStart().Length;

	private static string ListItemKind(string line)
	{
		var ordered = OrderedItemPattern.Match(line);
		if (ordered.Success)
		{
			return $"o{ordered.Groups[2].Value}";
		}

		var content = UnorderedItemPattern.Match(line).Groups[2].Value.TrimStart();
		if (content.StartsWith("[ ]", StringComparison.Ordinal))
		{
			return "task-open";
		}

		if (content.StartsWith("[x]", StringComparison.OrdinalIgnoreCase))
		{
			return "task-done";
		}

		return "bullet";
	}

	private static string Truncate(string token) => token.Length <= 60 ? token : token[..60] + "…";
}
