using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// The instruction-chain budget: every `AGENTS.md` in the tree is auto-loaded guidance, and all of
/// them share one 65,536-byte workspace budget with the root instruction file. A nested file carries
/// routing and binding rules — never knowledge — so it stays small and the whole chain stays inside
/// the budget; when the chain overflows, the loader drops whichever instruction file no longer fits.
/// The rule is `docs/AGENTS.md` §1, which also says what belongs in a node.
/// </summary>
public class AgentInstructionBudgetGateTests
{
	private const int WorkspaceInstructionBudgetBytes = 65536;
	private const int NestedInstructionCeilingBytes = 5120;

	private static readonly string[] SkippedDirectories = ["bin", "obj", ".git", ".vs", "reversing", "node_modules"];

	[Fact]
	public void EveryNestedInstructionFile_StaysUnderItsCeiling()
	{
		var files = InstructionFiles();
		Assert.True(
			files.Count >= 1,
			"the walk found no nested instruction file; the discovery filter stopped seeing the tree");
		Assert.Contains("docs/AGENTS.md", files);

		var oversized = Oversized(files.Select(relative => (relative, Size(relative))), NestedInstructionCeilingBytes);
		Assert.True(
			oversized.Count == 0,
			$"{oversized.Count} instruction file(s) carry more than guidance: {string.Join("; ", oversized)}");
	}

	[Fact]
	public void TheWholeChain_StaysInsideTheWorkspaceBudget()
	{
		var chain = new List<string> { "AGENTS.md" };
		chain.AddRange(InstructionFiles());
		if (File.Exists(RepositoryPaths.File("AGENTS.local.md")))
		{
			// Untracked, but it competes for the SAME loader budget: excluding it would report
			// headroom that does not exist.
			chain.Add("AGENTS.local.md");
		}

		var sizes = chain.Distinct(StringComparer.Ordinal).ToDictionary(relative => relative, Size);
		var total = sizes.Values.Sum();
		Assert.True(
			total <= WorkspaceInstructionBudgetBytes,
			$"the workspace instruction chain is {total} bytes, over the {WorkspaceInstructionBudgetBytes}-byte budget: {string.Join(", ", sizes.Select(entry => $"{entry.Key} {entry.Value}"))} — the loader drops whichever instruction file no longer fits");
	}

	[Fact]
	public void TheCeilingCheck_FlagsExactlyTheFilesOverTheLimit()
	{
		var files = new[]
		{
			("docs/AGENTS.md", NestedInstructionCeilingBytes),
			("docs/en/AGENTS.md", NestedInstructionCeilingBytes + 1)
		};

		var oversized = Oversized(files, NestedInstructionCeilingBytes);

		var flagged = Assert.Single(oversized);
		Assert.True(flagged.Contains("docs/en/AGENTS.md", StringComparison.Ordinal), flagged);
	}

	private static IReadOnlyList<string> Oversized(IEnumerable<(string Relative, int Size)> files, int ceiling) =>
		[.. files
			.Where(file => file.Size > ceiling)
			.OrderBy(file => file.Relative, StringComparer.Ordinal)
			.Select(file => $"{file.Relative} is {file.Size} bytes, over the {ceiling}-byte ceiling")];

	private static int Size(string relative) => (int)new FileInfo(RepositoryPaths.File(relative)).Length;

	/// <summary>Every `AGENTS.md` below the repository root; build output and the vendored tree carry none.</summary>
	internal static IReadOnlyList<string> InstructionFiles()
	{
		var found = new List<string>();
		var pending = new Stack<string>();
		pending.Push(RepositoryPaths.Root);
		while (pending.Count > 0)
		{
			var directory = pending.Pop();
			foreach (var child in Directory.EnumerateDirectories(directory))
			{
				if (!SkippedDirectories.Contains(Path.GetFileName(child), StringComparer.OrdinalIgnoreCase))
				{
					pending.Push(child);
				}
			}

			foreach (var file in Directory.EnumerateFiles(directory, "AGENTS.md"))
			{
				var relative = file.Substring(RepositoryPaths.Root.Length).TrimStart('\\', '/').Replace('\\', '/');
				if (!string.Equals(relative, "AGENTS.md", StringComparison.Ordinal))
				{
					found.Add(relative);
				}
			}
		}

		return [.. found.OrderBy(relative => relative, StringComparer.Ordinal)];
	}
}
