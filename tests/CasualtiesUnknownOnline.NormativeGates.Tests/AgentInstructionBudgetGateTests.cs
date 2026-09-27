using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// The instruction budget, in the two parts the loader actually charges. The always-present files —
/// the root `AGENTS.md` and the machine-local `AGENTS.local.md` beside it — share one 65,536-byte
/// workspace budget, and when they leave no room the loader drops the user-level instruction file
/// (observed in a session that reported "Workspace instruction budget 65536 bytes: omitted
/// ~/.dsh/AGENTS.md"). Every other instruction file is injected only while working in its directory,
/// so it is capped on its own instead: a nested tracked `AGENTS.md` and an area-local
/// `AGENTS.local.md` alike. That area file is what lets machine facts leave the root file without
/// costing every session — the cost is local, not gone. `docs/AGENTS.md` §1 says what belongs in a
/// node.
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
		// The area-local pair this repository relies on: a tracked router and the untracked facts file
		// beside it. If discovery stops seeing either, the measurement goes quietly blind.
		Assert.Contains("docs/acceptance/AGENTS.md", files);
		Assert.Contains("docs/acceptance/AGENTS.local.md", LocalInstructionFiles());

		var oversized = Oversized(
			files.Concat(LocalInstructionFiles()).Select(relative => (relative, Size(relative))),
			NestedInstructionCeilingBytes);
		Assert.True(
			oversized.Count == 0,
			$"{oversized.Count} instruction file(s) carry more than guidance: {string.Join("; ", oversized)}");
	}

	[Fact]
	public void TheAlwaysLoadedFiles_StayInsideTheWorkspaceBudget()
	{
		var alwaysLoaded = new List<string> { "AGENTS.md" };
		if (File.Exists(RepositoryPaths.File("AGENTS.local.md")))
		{
			// Untracked, but it is loaded beside the root file in every session: excluding it would
			// report headroom that does not exist.
			alwaysLoaded.Add("AGENTS.local.md");
		}

		var sizes = alwaysLoaded.ToDictionary(relative => relative, Size);
		var total = sizes.Values.Sum();
		Assert.True(
			total <= WorkspaceInstructionBudgetBytes,
			$"the always-loaded instruction files are {total} bytes, over the {WorkspaceInstructionBudgetBytes}-byte budget: {string.Join(", ", sizes.Select(entry => $"{entry.Key} {entry.Value}"))} — the loader drops the user-level instruction file when they leave no room");
	}

	[Fact]
	public void TheCeilingComparison_FlagsOnlySizesAboveTheLimit()
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
	internal static IReadOnlyList<string> InstructionFiles() => Discover("AGENTS.md", skipRepositoryRoot: true);

	/// <summary>Every `AGENTS.local.md` below the repository root except the root one: untracked machine facts, injected while working in their directory.</summary>
	internal static IReadOnlyList<string> LocalInstructionFiles() => Discover("AGENTS.local.md", skipRepositoryRoot: true);

	private static IReadOnlyList<string> Discover(string fileName, bool skipRepositoryRoot)
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

			foreach (var file in Directory.EnumerateFiles(directory, fileName))
			{
				var relative = file.Substring(RepositoryPaths.Root.Length).TrimStart('\\', '/').Replace('\\', '/');
				if (skipRepositoryRoot && relative.IndexOf('/') < 0)
				{
					continue;
				}

				found.Add(relative);
			}
		}

		return [.. found.OrderBy(relative => relative, StringComparer.Ordinal)];
	}
}
