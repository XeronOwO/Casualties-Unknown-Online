using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// The code sweep's coverage table (`docs/design/audit/sweep.md`) must cover the tree: every production `src/**/*.cs` file
/// outside an `obj/` directory is owned by exactly one block, each block's declared file count is the real count, and a
/// block's status is one of the three the table defines. Without this, "we walked all of the code" is a claim about a
/// document that can quietly stop matching the tree.
/// </summary>
public sealed class CodeSweepCoverageGateTests
{
	private static readonly string[] Statuses = ["unread", "read", "swept"];

	[Fact]
	public void EveryProductionFile_IsOwnedByExactlyOneBlock()
	{
		var blocks = Blocks();
		Assert.NotEmpty(blocks);

		var orphans = ProductionFiles().Where(file => Owner(file, blocks) is null).ToList();

		Assert.True(orphans.Count == 0, string.Join(Environment.NewLine, orphans));
	}

	[Fact]
	public void EveryBlock_DeclaresItsRealFileCountAndAValidStatus()
	{
		var blocks = Blocks();
		var files = ProductionFiles().ToList();
		var failures = new List<string>();
		foreach (var block in blocks)
		{
			if (!Statuses.Contains(block.Status, StringComparer.Ordinal))
			{
				failures.Add($"{block.Block}: status `{block.Status}`");
			}

			var actual = files.Count(file => string.Equals(Owner(file, blocks), block.Block, StringComparison.Ordinal));
			if (actual != block.Files)
			{
				failures.Add($"{block.Block}: declares {block.Files} files, the tree gives it {actual}");
			}
		}

		Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
	}

	[Fact]
	public void TheOwnershipRule_PrefersTheDeepestBlock()
	{
		List<(string Block, int Files, string Status)> blocks =
		[
			("src/A", 1, "unread"),
			("src/A/B", 1, "unread"),
		];

		Assert.Equal("src/A/B", Owner("src/A/B/C.cs", blocks));
		Assert.Equal("src/A", Owner("src/A/C.cs", blocks));
		Assert.Null(Owner("src/AB/C.cs", blocks));
	}

	/// <summary>The block whose path is the file's own directory or its deepest ancestor.</summary>
	private static string? Owner(string file, List<(string Block, int Files, string Status)> blocks) =>
		blocks
			.Select(block => block.Block)
			.Where(block => string.Equals(file, block, StringComparison.Ordinal) || file.StartsWith(block + "/", StringComparison.Ordinal))
			.OrderByDescending(block => block.Length)
			.ThenBy(block => block, StringComparer.Ordinal)
			.FirstOrDefault();

	private static IEnumerable<string> ProductionFiles() =>
		Directory
			.EnumerateFiles(Path.Combine(RepositoryPaths.Root, "src"), "*.cs", SearchOption.AllDirectories)
			.Where(path => !path.Contains("/obj/", StringComparison.Ordinal) && !path.Contains("\\obj\\", StringComparison.Ordinal))
			.Select(path => Path.GetRelativePath(RepositoryPaths.Root, path).Replace('\\', '/'))
			.OrderBy(path => path, StringComparer.Ordinal);

	/// <summary>The block rows of the sweep table, with the `src/` prefix the tree uses.</summary>
	private static List<(string Block, int Files, string Status)> Blocks()
	{
		var page = File.ReadAllText(Path.Combine(RepositoryPaths.Root, "docs", "design", "audit", "sweep.md"));
		var blocks = new List<(string Block, int Files, string Status)>();
		foreach (Match match in Regex.Matches(
			page,
			@"^\|\s*`(?<block>[^`]+)`\s*\|\s*(?<files>\d+)\s*\|\s*(?<status>\w+)\s*\|$",
			RegexOptions.Multiline))
		{
			blocks.Add(("src/" + match.Groups["block"].Value, int.Parse(match.Groups["files"].Value), match.Groups["status"].Value));
		}

		return blocks;
	}
}
