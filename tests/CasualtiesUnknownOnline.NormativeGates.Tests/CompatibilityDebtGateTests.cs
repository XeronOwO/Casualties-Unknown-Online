using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// Nothing in this tree has been released, so no branch may exist for an older version of it. The rule is not "no comments about legacy": the GAME has legacy
/// surfaces of its own and saying so is honest. What this gate refuses is the shape only OUR history can explain — an <c>[Obsolete]</c> attribute (a marker that
/// means "kept for a released caller") and a comment that announces a fallback for a client, save or protocol that never shipped.
/// </summary>
/// <remarks>
/// The matcher is pinned with both samples below, so the gate cannot quietly stop seeing the shape it was written for.
/// </remarks>
[Trait("Category", "Unit")]
public class CompatibilityDebtGateTests
{
	/// <summary>Phrases that announce debt to an older version of THIS project. A comment naming the game's own legacy modules is not matched.</summary>
	private static readonly Regex DebtPhrase = new(
		@"legacy fallback|backwards?[- ]compat|compat(ibility)? branch|for old clients?|for old data|pre-namespace semantics|older protocol version",
		RegexOptions.Compiled | RegexOptions.IgnoreCase);

	/// <summary>A released-API marker: nothing is released, so its presence means a caller that does not exist is being kept alive.</summary>
	private static readonly Regex ObsoleteAttribute = new(@"\[Obsolete(\]|\()", RegexOptions.Compiled);

	[Fact]
	public void NoSourceFile_KeepsACallerThatNeverShipped()
	{
		var failures = new List<string>();
		foreach (var path in SourceFiles())
		{
			var lines = File.ReadAllLines(path);
			for (var index = 0; index < lines.Length; index++)
			{
				if (ObsoleteAttribute.IsMatch(lines[index]) || DebtPhrase.IsMatch(lines[index]))
				{
					failures.Add($"{Relative(path)}:{index + 1}: {lines[index].Trim()}");
				}
			}
		}

		Assert.True(
			failures.Count == 0,
			"src/ keeps a shape for a version of this project that never shipped (nothing has been released; delete it or record the live constraint it serves):"
				+ Environment.NewLine + string.Join(Environment.NewLine, failures));
	}

	[Fact]
	public void TheMatcher_RefusesDebt_AndPassesTheGamesOwnLegacy()
	{
		// Refused: the shapes only our own history can explain.
		Assert.Matches(DebtPhrase, "// Legacy fallback: extremely old clone renders may still carry an id.");
		Assert.Matches(DebtPhrase, "// kept for backwards compatibility with the first build");
		Assert.Matches(DebtPhrase, "// compat branch: old clients send the flat id");
		Assert.Matches(ObsoleteAttribute, "[Obsolete(\"use the definition contract\")]");

		// Passed: the game's own legacy surfaces, which are not this project's debt.
		Assert.DoesNotMatch(DebtPhrase, "/// legacy protobuf messages a version adapter and the Game Adapter still speak");
		Assert.DoesNotMatch(DebtPhrase, "/// legacy work and forward the limb conversion here.");
		Assert.DoesNotMatch(ObsoleteAttribute, "// the game's InputLegacyModule is not ours to simplify");
	}

	private static IEnumerable<string> SourceFiles() =>
		Directory.GetFiles(RepositoryPaths.File("src"), "*.cs", SearchOption.AllDirectories)
			.Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
			.Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
			.OrderBy(path => path, StringComparer.Ordinal);

	private static string Relative(string path) => Path.GetRelativePath(RepositoryPaths.Root, path);
}
