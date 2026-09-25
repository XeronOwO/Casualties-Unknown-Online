using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// The documentation tree: `docs/en` and `docs/zh` hold the same page set at the same paths, every
/// relative link points at a file that exists, every content page carries the shape the standard
/// fixes, and every pair is recorded in `docs/standard/alignment.txt` at the contents it was
/// confirmed with. The rules are `docs/AGENTS.md`; this gate is what keeps them from decaying.
/// A green run proves presence and shape — it cannot judge whether a page teaches.
/// </summary>
public class DocumentationTreeGateTests
{
	/// <summary>Close to the real page count per block, so losing half the tree fails here.</summary>
	private const int MinimumPagePairs = 18;

	/// <summary>The two block indexes plus the four section indexes of each block.</summary>
	private const int MinimumIndexPages = 10;

	private static readonly string[] ScannedLooseFiles =
		["docs/README.md", "docs/AGENTS.md", "docs/standard/README.md", "docs/contracts/README.md", "docs/en/AGENTS.md", "docs/zh/AGENTS.md"];

	[Fact]
	public void EveryPage_HasItsCounterpartInTheOtherBlock()
	{
		var english = Pages("docs/en");
		var chinese = Pages("docs/zh");
		Assert.True(
			english.Count >= MinimumPagePairs && chinese.Count >= MinimumPagePairs,
			$"discovery found {english.Count} English and {chinese.Count} Chinese pages; the floor is {MinimumPagePairs} — the discovery filter stopped seeing the tree");

		var failures = ParityFailures(english, chinese);
		Assert.True(failures.Count == 0, Describe("page parity", failures));
	}

	[Fact]
	public void EveryRelativeLink_PointsAtAFileThatExists()
	{
		var files = ScannedLooseFiles
			.Concat(Pages("docs/en"))
			.Concat(Pages("docs/zh"))
			.ToList();

		var failures = new List<string>();
		foreach (var relative in files)
		{
			foreach (var target in LinkTargets(File.ReadAllText(RepositoryPaths.File(relative))))
			{
				if (target.StartsWith("http", StringComparison.OrdinalIgnoreCase) || target.StartsWith('#'))
				{
					continue;
				}

				var path = target.Split('#')[0];
				if (path.Length == 0)
				{
					continue;
				}

				var directory = Path.GetDirectoryName(RepositoryPaths.File(relative)) ?? RepositoryPaths.Root;
				var resolved = Path.GetFullPath(Path.Combine(directory, path));
				if (!File.Exists(resolved) && !Directory.Exists(resolved))
				{
					failures.Add($"{relative} → {target}");
				}
			}
		}

		Assert.True(failures.Count == 0, Describe("link targets", failures));
	}

	[Fact]
	public void EveryPage_CarriesTheDocumentShape()
	{
		var failures = new List<string>();
		foreach (var relative in Pages("docs/en").Concat(Pages("docs/zh")))
		{
			failures.AddRange(ShapeFailures(relative, File.ReadAllLines(RepositoryPaths.File(relative))));
		}

		Assert.True(failures.Count == 0, Describe("page shape", failures));
	}

	[Fact]
	public void EveryPair_IsRecordedAtItsCurrentContents()
	{
		var record = RepositoryPaths.File("docs/standard/alignment.txt");
		Assert.True(File.Exists(record), "docs/standard/alignment.txt is missing");

		var rows = new List<(string Path, string Recorded, string Actual)>();
		var recorded = new HashSet<string>(StringComparer.Ordinal);
		foreach (var line in File.ReadAllLines(record))
		{
			var trimmed = line.Trim();
			if (trimmed.Length == 0 || trimmed.StartsWith('#'))
			{
				continue;
			}

			var cells = trimmed.Split('|').Select(cell => cell.Trim()).ToArray();
			Assert.True(cells.Length == 4, $"alignment.txt row is not <en path> | <hash> | <zh path> | <hash>: {trimmed}");
			foreach (var (path, hash) in new[] { (cells[0], cells[1]), (cells[2], cells[3]) })
			{
				recorded.Add(path);
				rows.Add((path, hash, File.Exists(RepositoryPaths.File(path)) ? BlobHash(File.ReadAllBytes(RepositoryPaths.File(path))) : "(missing)"));
			}
		}

		var failures = new List<string>(HashFailures(rows));
		foreach (var relative in Pages("docs/en").Concat(Pages("docs/zh")).Where(relative => !recorded.Contains(relative)))
		{
			failures.Add($"{relative}: no row in alignment.txt");
		}

		Assert.True(failures.Count == 0, Describe("pair alignment record", failures));
	}

	[Fact]
	public void TheParityCheck_FlagsAOneSidedPage()
	{
		var failures = ParityFailures(["docs/en/start/a.md", "docs/en/start/b.md"], ["docs/zh/start/a.md"]);
		var failure = Assert.Single(failures);
		Assert.Contains("docs/en/start/b.md", failure, StringComparison.Ordinal);

		Assert.Empty(ParityFailures(["docs/en/start/a.md"], ["docs/zh/start/a.md"]));
	}

	[Fact]
	public void TheShapeCheck_FlagsAMissingRelatedReading()
	{
		var page = new[]
		{
			"# Title",
			string.Empty,
			"[Documentation](../README.md) > [Start](README.md) > Title",
			string.Empty,
			"---",
			string.Empty,
			"Body.",
			string.Empty,
			"## Related reading",
			string.Empty,
			"- [Start here](README.md)",
			string.Empty,
			"---",
			string.Empty,
			"[Documentation](../README.md) > [Start](README.md) > Title"
		};

		Assert.Empty(ShapeFailures("docs/en/start/title.md", page));

		var withoutRelated = page.Where(line => line != "## Related reading").ToArray();
		Assert.Contains(ShapeFailures("docs/en/start/title.md", withoutRelated), failure => failure.Contains("Related reading", StringComparison.Ordinal));

		var withoutTail = page.Take(page.Length - 2).ToArray();
		Assert.Contains(ShapeFailures("docs/en/start/title.md", withoutTail), failure => failure.Contains("tail breadcrumb", StringComparison.Ordinal));

		var withoutBreaks = page.Where(line => line != "---").ToArray();
		Assert.Contains(ShapeFailures("docs/en/start/title.md", withoutBreaks), failure => failure.Contains("thematic break", StringComparison.Ordinal));

		// A break that drifted into the body is caught on position, at each end.
		var tailMisplaced = page.ToArray();
		tailMisplaced[^4] = "Body line.";
		tailMisplaced[^5] = "---";
		Assert.Contains(ShapeFailures("docs/en/start/title.md", tailMisplaced), failure => failure.Contains("above the tail breadcrumb", StringComparison.Ordinal));

		// An index page names its place and lists the section's pages; it needs no Related reading.
		var index = new[]
		{
			"# Start here",
			string.Empty,
			"[Documentation](../README.md) > Start here",
			string.Empty,
			"---",
			string.Empty,
			"- [What CUO is](what-is-cuo.md)",
			string.Empty,
			"---",
			string.Empty,
			"[Documentation](../README.md) > Start here"
		};
		Assert.Empty(ShapeFailures("docs/en/start/README.md", index));
		Assert.Contains(
			ShapeFailures("docs/en/start/README.md", ["# Start here", string.Empty, "Nothing to read yet."]),
			failure => failure.Contains("links to no page", StringComparison.Ordinal));
		Assert.Contains(
			ShapeFailures("docs/en/start/README.md", ["# Start here", string.Empty, "- [What CUO is](what-is-cuo.md)"]),
			failure => failure.Contains("head breadcrumb", StringComparison.Ordinal));
		Assert.Contains(
			ShapeFailures("docs/en/start/README.md", [.. index.Where(line => line != "---")]),
			failure => failure.Contains("thematic break", StringComparison.Ordinal));

		// A block index is the overview itself: its own label, with no link back to the switcher.
		var blockIndex = new[]
		{
			"# CUO documentation — English",
			string.Empty,
			"Documentation",
			string.Empty,
			"---",
			string.Empty,
			"- [Start here](start/README.md)",
			string.Empty,
			"---",
			string.Empty,
			"Documentation"
		};
		Assert.Empty(ShapeFailures("docs/en/README.md", blockIndex));
		Assert.Contains(
			ShapeFailures("docs/en/README.md", ["# CUO documentation — English", string.Empty, "- [Start here](start/README.md)"]),
			failure => failure.Contains("head breadcrumb", StringComparison.Ordinal));
	}

	[Fact]
	public void EveryIndex_NamesItsOwnDirectory()
	{
		var pages = IndexPages();
		Assert.True(
			pages.Count >= MinimumIndexPages,
			$"discovery found {pages.Count} index pages; the floor is {MinimumIndexPages} — the discovery filter stopped seeing the tree");

		var failures = IndexPathFailures(pages, relative => File.ReadAllText(RepositoryPaths.File(relative)));
		Assert.True(failures.Count == 0, Describe("index pages naming their directory", failures));
	}

	[Fact]
	public void TheIndexPathCheck_FlagsAnIndexWithoutItsPath()
	{
		var withPath = "# How to" + Environment.NewLine + "Read `docs/en/how-to/` first.";
		var withoutPath = "# How to" + Environment.NewLine + "One task per page.";
		Assert.Empty(IndexPathFailures(["docs/en/how-to/README.md"], _ => withPath));
		Assert.Contains(
			IndexPathFailures(["docs/en/how-to/README.md"], _ => withoutPath),
			failure => failure.Contains("docs/en/how-to/README.md", StringComparison.Ordinal));
	}

	[Fact]
	public void EveryChinesePage_UsesChinesePunctuation()
	{
		var pages = Pages("docs/zh").Concat(["docs/README.md"]).ToList();
		Assert.True(
			pages.Count >= MinimumPagePairs,
			$"discovery found {pages.Count} Chinese pages; the floor is {MinimumPagePairs} — the discovery filter stopped seeing the tree");

		var failures = new List<string>();
		foreach (var relative in pages)
		{
			failures.AddRange(AsciiPunctuationFailures(relative, File.ReadAllLines(RepositoryPaths.File(relative))));
		}

		Assert.True(failures.Count == 0, Describe("Chinese prose punctuation", failures));
	}

	[Fact]
	public void ThePunctuationCheck_FlagsAsciiPunctuationInProse()
	{
		Assert.Empty(AsciiPunctuationFailures("docs/zh/start/a.md", ["中文，句子：正确。", "Two mirrored trees, one page set: 中文",
			"`code, with: ascii` stays.", "`CanWrite` 是 false，`TrySet` 返回 false。"]));
		Assert.Contains(
			AsciiPunctuationFailures("docs/zh/start/a.md", ["中文,句子。"]),
			failure => failure.Contains("docs/zh/start/a.md", StringComparison.Ordinal));
	}

	[Fact]
	public void TheAlignmentCheck_FlagsAnEditedSide()
	{
		var hash = new string('a', 40);
		Assert.Empty(HashFailures([("docs/en/start/a.md", hash, hash)]));
		var failure = Assert.Single(HashFailures([("docs/zh/start/a.md", hash, new string('b', 40))]));
		Assert.Contains("docs/zh/start/a.md", failure, StringComparison.Ordinal);
	}

	[Fact]
	public void TheAlignmentHash_IgnoresTheLineEndingStyleOfTheWorkingTree()
	{
		// `.gitattributes` checks the tree out with CRLF (`* text=auto eol=crlf`) while the index — and
		// `git hash-object`, the workflow the registry documents — hold the LF form. A raw-byte hash would
		// disagree with every recorded pair on any checkout git itself performs.
		var unix = Encoding.UTF8.GetBytes("first line\nsecond line\n");
		var windows = Encoding.UTF8.GetBytes("first line\r\nsecond line\r\n");

		Assert.Equal(BlobHash(unix), BlobHash(windows));
	}

	/// <summary>Every page of one block, repository-relative, without the block's instruction file.</summary>
	private static IReadOnlyList<string> Pages(string block) =>
		[.. Directory
			.EnumerateFiles(RepositoryPaths.File(block), "*.md", SearchOption.AllDirectories)
			.Select(Relative)
			.Where(relative => !relative.EndsWith("/AGENTS.md", StringComparison.Ordinal))
			.OrderBy(relative => relative, StringComparer.Ordinal)];

	private static string Relative(string absolute) =>
		absolute.Substring(RepositoryPaths.Root.Length).TrimStart('\\', '/').Replace('\\', '/');

	private static string Counterpart(string relative) =>
		relative.StartsWith("docs/en/", StringComparison.Ordinal)
			? "docs/zh/" + relative.Substring("docs/en/".Length)
			: "docs/en/" + relative.Substring("docs/zh/".Length);

	private static IReadOnlyList<string> ParityFailures(IReadOnlyList<string> english, IReadOnlyList<string> chinese)
	{
		var failures = new List<string>();
		failures.AddRange(english.Where(page => !chinese.Contains(Counterpart(page), StringComparer.Ordinal)).Select(page => $"{page}: no Chinese counterpart"));
		failures.AddRange(chinese.Where(page => !english.Contains(Counterpart(page), StringComparer.Ordinal)).Select(page => $"{page}: no English counterpart"));
		return failures;
	}

	/// <summary>The ASCII forms of the connectives a Chinese sentence writes in full width.</summary>
	private static readonly char[] AsciiConnectives = [',', ':', ';', '!', '?'];

	/// <summary>
	/// `docs/AGENTS.md` §3: Chinese punctuation joins Chinese text. The gate catches the plain defect —
	/// an ASCII connective between two Chinese characters (`中文,句子`) — and deliberately no more: an
	/// English phrase inside a Chinese page keeps its own punctuation (`Casualties Unknown: Online`,
	/// `是 false，每次调用`), so judging that stays a review duty.
	/// </summary>
	private static IReadOnlyList<string> AsciiPunctuationFailures(string relative, IReadOnlyList<string> lines)
	{
		var failures = new List<string>();
		var fenced = false;
		for (var number = 0; number < lines.Count; number++)
		{
			var line = lines[number];
			if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
			{
				fenced = !fenced;
				continue;
			}

			if (fenced)
			{
				continue;
			}

			foreach (var index in ProseIndices(line))
			{
				if (AsciiConnectives.Contains(line[index]) && BetweenChinese(line, index))
				{
					failures.Add($"{relative}:{number + 1}: ASCII `{line[index]}` between Chinese characters — use the Chinese form");
					break;
				}
			}
		}

		return failures;
	}

	/// <summary>The positions of a line that are prose: outside an inline code span and a link target.</summary>
	private static IEnumerable<int> ProseIndices(string line)
	{
		var inCode = false;
		for (var index = 0; index < line.Length; index++)
		{
			var ch = line[index];
			if (ch == '`')
			{
				inCode = !inCode;
				continue;
			}

			if (inCode)
			{
				continue;
			}

			if (ch == ']' && index + 1 < line.Length && line[index + 1] == '(')
			{
				var close = line.IndexOf(')', index);
				index = close < 0 ? line.Length - 1 : close;
				continue;
			}

			yield return index;
		}
	}

	/// <summary>True when the nearest non-space character on each side is a Chinese one.</summary>
	private static bool BetweenChinese(string line, int index)
	{
		var left = index - 1;
		while (left >= 0 && line[left] == ' ')
		{
			left--;
		}

		var right = index + 1;
		while (right < line.Length && line[right] == ' ')
		{
			right++;
		}

		return left >= 0 && right < line.Length && IsCjk(line[left]) && IsCjk(line[right]);
	}

	/// <summary>An ideograph or a full-width form: a character that carries Chinese text.</summary>
	private static bool IsCjk(char ch) =>
		(ch >= '\u3400' && ch <= '\u9fff') || (ch >= '\u3000' && ch <= '\u303f') || (ch >= '\uff00' && ch <= '\uffef');

	/// <summary>The index of a block or of one of its sections: `docs/en/README.md`, `docs/zh/how-to/README.md`.</summary>
	private static IReadOnlyList<string> IndexPages() =>
		[.. Pages("docs/en").Concat(Pages("docs/zh")).Where(relative => relative.EndsWith("/README.md", StringComparison.Ordinal))];

	/// <summary>
	/// An index states its own directory — `docs/en/how-to/` — which is the rule in `docs/AGENTS.md` §1:
	/// the reader (and an agent looking for the files) gets the path in the repository, not a guess.
	/// </summary>
	private static IReadOnlyList<string> IndexPathFailures(IReadOnlyList<string> pages, Func<string, string> read)
	{
		var failures = new List<string>();
		foreach (var relative in pages)
		{
			var directory = relative[..(relative.LastIndexOf('/') + 1)];
			if (!read(relative).Contains(directory, StringComparison.Ordinal))
			{
				failures.Add($"{relative}: names no directory — an index states its own path, `{directory}`");
			}
		}

		return failures;
	}

	private static IReadOnlyList<string> ShapeFailures(string relative, IReadOnlyList<string> lines)
	{
		if (relative.EndsWith("/README.md", StringComparison.Ordinal))
		{
			// An index names its place in the tree and lists the section's pages.
			var indexFailures = new List<string>();
			var named = BlockOverviewLabel.TryGetValue(relative[..relative.LastIndexOf('/')], out var label)
				? lines.Take(8).Any(line => string.Equals(line.Trim(), label, StringComparison.Ordinal))
				: lines.Take(8).Any(IsBreadcrumb);
			if (!named)
			{
				indexFailures.Add($"{relative}: no head breadcrumb in the first lines");
			}

			if (!lines.Any(line => line.Contains("](", StringComparison.Ordinal) && line.Contains(".md)", StringComparison.Ordinal)))
			{
				indexFailures.Add($"{relative}: the index links to no page");
			}

			AddSeparatorFailures(relative, lines, indexFailures);
			return indexFailures;
		}

		var chinese = relative.StartsWith("docs/zh/", StringComparison.Ordinal);
		var failures = new List<string>();
		if (!lines.Take(8).Any(IsBreadcrumb))
		{
			failures.Add($"{relative}: no head breadcrumb in the first lines");
		}

		var heading = chinese ? "## 相关阅读" : "## Related reading";
		if (!lines.Any(line => line.Trim() == heading))
		{
			failures.Add($"{relative}: no `{heading}` section");
		}

		if (!lines.Where(line => line.Trim().Length > 0).Reverse().Take(2).Any(IsBreadcrumb))
		{
			failures.Add($"{relative}: no tail breadcrumb");
		}

		AddSeparatorFailures(relative, lines, failures);
		return failures;
	}

	/// <summary>
	/// `docs/AGENTS.md` §2: the page carries exactly two thematic breaks — one under the head
	/// breadcrumb (title, blank, breadcrumb, blank, break) and one two lines above the tail breadcrumb
	/// (break, blank, breadcrumb). A break that drifted into the body fails on position.
	/// </summary>
	private static void AddSeparatorFailures(string relative, IReadOnlyList<string> lines, List<string> failures)
	{
		var breaks = Enumerable.Range(0, lines.Count).Where(index => lines[index].Trim() == "---").ToList();
		if (breaks.Count != 2)
		{
			failures.Add($"{relative}: {breaks.Count} thematic break(s) — a page carries exactly two, one under the head breadcrumb and one above the tail breadcrumb");
			return;
		}

		var lastContent = lines.Count - 1;
		while (lastContent >= 0 && lines[lastContent].Trim().Length == 0)
		{
			lastContent--;
		}

		if (breaks[0] != 4)
		{
			failures.Add($"{relative}: the first thematic break does not sit under the head breadcrumb");
		}

		if (breaks[1] != lastContent - 2)
		{
			failures.Add($"{relative}: the last thematic break does not sit above the tail breadcrumb");
		}
	}

	private static bool IsBreadcrumb(string line) =>
		line.Contains("](../README.md)", StringComparison.Ordinal) && line.Contains('>');

	/// <summary>A block's overview label: the block index names itself with it, and every page's breadcrumb starts with it.</summary>
	private static readonly Dictionary<string, string> BlockOverviewLabel = new(StringComparer.Ordinal)
	{
		["docs/en"] = "Documentation",
		["docs/zh"] = "文档总览",
	};

	private static IReadOnlyList<string> HashFailures(IEnumerable<(string Path, string Recorded, string Actual)> rows) =>
		[.. rows
			.Where(row => !string.Equals(row.Recorded, row.Actual, StringComparison.Ordinal))
			.Select(row => $"{row.Path}: recorded {row.Recorded} but the file hashes to {row.Actual} — one side was edited without re-recording the pair")];

	private static IReadOnlyList<string> LinkTargets(string text)
	{
		var targets = new List<string>();
		var index = 0;
		while (index < text.Length)
		{
			var open = text.IndexOf("](", index, StringComparison.Ordinal);
			if (open < 0)
			{
				break;
			}

			var close = text.IndexOf(')', open);
			if (close < 0)
			{
				break;
			}

			targets.Add(text.Substring(open + 2, close - open - 2).Trim());
			index = close + 1;
		}

		return targets;
	}

	/// <summary>The git blob hash of a file's contents: `blob &lt;length&gt;\0&lt;content&gt;`, SHA-1.</summary>
	/// <remarks>
	/// The contents are hashed as git stores them, with CRLF folded to LF: `.gitattributes` checks this
	/// tree out with CRLF while the index holds LF, and `git hash-object` — the workflow the registry
	/// documents — hashes the LF form. Hashing the raw bytes would turn every recorded pair red on any
	/// checkout git itself performs, which is the one environment the record has to survive.
	/// </remarks>
	private static string BlobHash(byte[] content)
	{
		var stored = AsStored(content);
		var header = Encoding.ASCII.GetBytes($"blob {stored.Length}\0");
		var buffer = new byte[header.Length + stored.Length];
		header.CopyTo(buffer, 0);
		stored.CopyTo(buffer, header.Length);
		return Convert.ToHexString(SHA1.HashData(buffer)).ToLowerInvariant();
	}

	/// <summary>The bytes git stores: CRLF folded to LF, everything else left alone.</summary>
	private static byte[] AsStored(byte[] content)
	{
		if (!content.Contains((byte)'\r'))
		{
			return content;
		}

		var buffer = new byte[content.Length];
		var written = 0;
		for (var index = 0; index < content.Length; index++)
		{
			if (content[index] == (byte)'\r' && index + 1 < content.Length && content[index + 1] == (byte)'\n')
			{
				continue;
			}

			buffer[written++] = content[index];
		}

		return buffer[..written];
	}

	private static string Describe(string check, IReadOnlyList<string> failures) =>
		$"{check}: {failures.Count} violation(s){Environment.NewLine}{string.Join(Environment.NewLine, failures)}";
}
