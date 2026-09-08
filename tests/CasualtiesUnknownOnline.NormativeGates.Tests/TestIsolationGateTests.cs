using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// Parallel-safety gate for the test suite. xUnit v2 gives every test class its
/// own collection and runs different collections in parallel, so a test that
/// writes a process-global static field — a <c>SetValue(null, ...)</c> on a
/// reflection field handle — must share the single <c>GameAssembly</c>
/// collection with every other test that touches the same game-assembly/Unity
/// static state. Without the shared collection two classes race on the same
/// field (one replaces the table between the other's arrange and assert) and
/// the suite turns order-dependent. The rule is deliberately conservative: every
/// static write needs the collection. Add an allowlist entry here only with a
/// written reason.
/// </summary>
public class TestIsolationGateTests
{
	private const string TestProjectDir = "tests/CasualtiesUnknownOnline.Tests";
	private const string CollectionAttribute = "[Collection(GameAssemblyCollection.Name)]";
	private const string CollectionDeclaration = "[CollectionDefinition(Name)]";
	private const string CollectionName = "public const string Name = \"GameAssembly\";";

	[Fact]
	public void StaticGameStateMutations_JoinTheGameAssemblyCollection()
	{
		var failures = new List<string>();
		foreach (var file in EnumerateTestSources())
		{
			var text = File.ReadAllText(file);
			if (!text.Contains("SetValue(null", StringComparison.Ordinal))
			{
				continue;
			}

			if (!text.Contains(CollectionAttribute, StringComparison.Ordinal))
			{
				failures.Add($"{Relative(file)} writes a process-global static field but is not in {CollectionAttribute}; "
					+ "join the GameAssembly collection so xUnit cannot run it beside another test that touches the same state.");
			}
		}

		Assert.True(failures.Count == 0,
			"Test parallel-safety gate failed" + Environment.NewLine + string.Join(Environment.NewLine, failures));
	}

	[Fact]
	public void GameAssemblyCollection_IsDeclaredWithTheExpectedName()
	{
		var declarationFile = RepositoryPaths.File($"{TestProjectDir}/Patching/GameAssemblyCollection.cs");
		Assert.True(File.Exists(declarationFile), $"missing {Relative(declarationFile)}");
		var text = File.ReadAllText(declarationFile);
		Assert.True(text.Contains(CollectionDeclaration, StringComparison.Ordinal),
			$"{Relative(declarationFile)} must declare {CollectionDeclaration}");
		Assert.True(text.Contains(CollectionName, StringComparison.Ordinal),
			$"{Relative(declarationFile)} must declare {CollectionName}");
	}

	private static IEnumerable<string> EnumerateTestSources() =>
		Directory.EnumerateFiles(RepositoryPaths.File(TestProjectDir), "*.cs", SearchOption.AllDirectories)
			.Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
				&& !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

	private static string Relative(string path) => Path.GetRelativePath(RepositoryPaths.Root, path);
}
