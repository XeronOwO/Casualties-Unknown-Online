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
	/// <summary>Far below the real page count: a discovery filter that stops seeing the tree fails here.</summary>
	private const int MinimumPagePairs = 10;

	private static readonly string[] ScannedLooseFiles = ["docs/README.md", "docs/AGENTS.md", "docs/standard/README.md"];

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
			"[Documentation](../../README.md) > [Start](README.md) > Title",
			string.Empty,
			"Body.",
			string.Empty,
			"## Related reading",
			string.Empty,
			"- [Start here](README.md)",
			string.Empty,
			"[Documentation](../../README.md) > [Start](README.md) > Title"
		};

		Assert.Empty(ShapeFailures("docs/en/start/title.md", page));

		var withoutRelated = page.Where(line => line != "## Related reading").ToArray();
		Assert.Contains(ShapeFailures("docs/en/start/title.md", withoutRelated), failure => failure.Contains("Related reading", StringComparison.Ordinal));

		var withoutTail = page.Take(page.Length - 2).ToArray();
		Assert.Contains(ShapeFailures("docs/en/start/title.md", withoutTail), failure => failure.Contains("tail breadcrumb", StringComparison.Ordinal));

		// An index page is the exception: it is a list of links, not a page with a shape.
		Assert.Empty(ShapeFailures("docs/en/start/README.md", ["# Start here", string.Empty, "- [What CUO is](what-is-cuo.md)"]));
		Assert.Contains(
			ShapeFailures("docs/en/start/README.md", ["# Start here", string.Empty, "Nothing to read yet."]),
			failure => failure.Contains("links to no page", StringComparison.Ordinal));
	}

	[Fact]
	public void TheAlignmentCheck_FlagsAnEditedSide()
	{
		var hash = new string('a', 40);
		Assert.Empty(HashFailures([("docs/en/start/a.md", hash, hash)]));
		var failure = Assert.Single(HashFailures([("docs/zh/start/a.md", hash, new string('b', 40))]));
		Assert.Contains("docs/zh/start/a.md", failure, StringComparison.Ordinal);
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

	private static IReadOnlyList<string> ShapeFailures(string relative, IReadOnlyList<string> lines)
	{
		if (relative.EndsWith("/README.md", StringComparison.Ordinal))
		{
			// An index lists the section's pages; it carries no breadcrumb of its own.
			return lines.Any(line => line.Contains("](", StringComparison.Ordinal) && line.Contains(".md)", StringComparison.Ordinal))
				? []
				: [$"{relative}: the index links to no page"];
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

		return failures;
	}

	private static bool IsBreadcrumb(string line) =>
		line.Contains("](../../README.md)", StringComparison.Ordinal) && line.Contains('>');

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

	/// <summary>The git blob hash of a file's bytes: `blob &lt;length&gt;\0&lt;content&gt;`, SHA-1.</summary>
	private static string BlobHash(byte[] content)
	{
		var header = Encoding.ASCII.GetBytes($"blob {content.Length}\0");
		var buffer = new byte[header.Length + content.Length];
		header.CopyTo(buffer, 0);
		content.CopyTo(buffer, header.Length);
		return Convert.ToHexString(SHA1.HashData(buffer)).ToLowerInvariant();
	}

	private static string Describe(string check, IReadOnlyList<string> failures) =>
		$"{check}: {failures.Count} violation(s){Environment.NewLine}{string.Join(Environment.NewLine, failures)}";
}
