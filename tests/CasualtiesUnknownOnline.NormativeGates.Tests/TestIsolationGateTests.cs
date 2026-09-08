using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// Parallel-safety gate for the test suite. xUnit v2 gives every test class its
/// own collection and runs different collections in parallel, so a test class
/// that writes process-global state — a reflection <c>SetValue(null, ...)</c> on
/// a static field, which is how the game-assembly contract tests mutate the
/// loaded game/Unity statics — must join the non-parallel <c>GameAssembly</c>
/// collection. Without that, two classes race on the same static field (one
/// replaces the table between the other's arrange and assert) and the suite
/// turns order-dependent.
///
/// The gate parses every test source with Roslyn and checks each test class
/// (a class with a <c>[Fact]</c>/<c>[Theory]</c> method) that performs a static
/// <c>SetValue</c>. Review-enforced blind spots that no source gate can see
/// without semantic references to the game assemblies: a direct static
/// assignment through the GameRef alias, applying a Harmony patch, and calling
/// production code that mutates a process-global static. Keep those out of the
/// suite, or join the collection explicitly.
/// </summary>
public class TestIsolationGateTests
{
	private const string TestProjectDir = "tests/CasualtiesUnknownOnline.Tests";
	private const string CollectionName = "GameAssembly";
	private const string CollectionAttribute = "[Collection(GameAssemblyCollection.Name)]";

	[Fact]
	public void StaticGameStateMutations_JoinTheGameAssemblyCollection()
	{
		var failures = new List<string>();
		foreach (var file in EnumerateTestSources())
		{
			failures.AddRange(FindUnisolatedStaticWriters(File.ReadAllText(file), Relative(file)));
		}

		Assert.True(failures.Count == 0,
			"Test parallel-safety gate failed" + Environment.NewLine + string.Join(Environment.NewLine, failures));
	}

	[Fact]
	public void StaticGameStateMutationDetection_FlagsUnisolatedTestClasses()
	{
		// The gate's own negative/positive contract: it must fail on a static
		// write in a test class without the collection, and must ignore both the
		// compliant form and a non-test helper class.
		const string Violating = """
			using Xunit;
			namespace X;
			public class A
			{
				[Fact]
				public void T() => field.SetValue(null, table);
			}
			""";
		const string Compliant = """
			using Xunit;
			namespace X;
			[Collection(GameAssemblyCollection.Name)]
			public class A
			{
				[Fact]
				public void T() => field.SetValue(null, table);
			}
			""";
		const string InstanceWrite = """
			using Xunit;
			namespace X;
			public class A
			{
				[Fact]
				public void T() => field.SetValue(instance, table);
			}
			""";
		const string HelperOnly = """
			namespace X;
			internal static class Helper
			{
				internal static void T() => field.SetValue(null, table);
			}
			""";

		Assert.NotEmpty(FindUnisolatedStaticWriters(Violating, "violating.cs"));
		Assert.Empty(FindUnisolatedStaticWriters(Compliant, "compliant.cs"));
		Assert.Empty(FindUnisolatedStaticWriters(InstanceWrite, "instance.cs"));
		Assert.Empty(FindUnisolatedStaticWriters(HelperOnly, "helper.cs"));
	}

	[Fact]
	public void GameAssemblyCollection_IsDeclaredAsTheNonParallelCollection()
	{
		var declarationFile = RepositoryPaths.File($"{TestProjectDir}/Patching/GameAssemblyCollection.cs");
		Assert.True(File.Exists(declarationFile), $"missing {Relative(declarationFile)}");
		var text = File.ReadAllText(declarationFile);
		Assert.True(text.Contains("[CollectionDefinition(Name, DisableParallelization = true)]", StringComparison.Ordinal),
			$"{Relative(declarationFile)} must declare the collection as non-parallel "
			+ "(DisableParallelization = true), otherwise a reader in another collection can still observe a half-applied static write.");
		Assert.True(text.Contains($"public const string Name = \"{CollectionName}\";", StringComparison.Ordinal),
			$"{Relative(declarationFile)} must declare public const string Name = \"{CollectionName}\";");
	}

	/// <summary>Test classes (a class with a <c>[Fact]</c>/<c>[Theory]</c> method)
	/// that write a static field through reflection without joining the
	/// GameAssembly collection. A static write is <c>SetValue(null, ...)</c> — the
	/// first argument, positional or named <c>obj</c>, is a null literal
	/// (optionally <c>null!</c>).</summary>
	private static IReadOnlyList<string> FindUnisolatedStaticWriters(string source, string label)
	{
		var failures = new List<string>();
		var root = CSharpSyntaxTree.ParseText(source).GetRoot();

		foreach (var classDeclaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
		{
			if (!IsTestClass(classDeclaration))
			{
				continue;
			}

			var writesStaticState = classDeclaration.DescendantNodes().OfType<InvocationExpressionSyntax>()
				.Any(IsStaticSetValue);
			if (!writesStaticState)
			{
				continue;
			}

			var inCollection = classDeclaration.AttributeLists.SelectMany(list => list.Attributes)
				.Any(attribute => attribute.Name.ToString() is "Collection" or "CollectionAttribute");
			if (!inCollection)
			{
				failures.Add($"{label}: test class '{classDeclaration.Identifier.ValueText}' writes a process-global static "
					+ $"but is not in {CollectionAttribute}; xUnit runs different collections in parallel.");
			}
		}

		return failures;
	}

	private static bool IsTestClass(ClassDeclarationSyntax classDeclaration) =>
		classDeclaration.Members.OfType<MethodDeclarationSyntax>()
			.SelectMany(method => method.AttributeLists)
			.SelectMany(list => list.Attributes)
			.Any(attribute => attribute.Name.ToString() is "Fact" or "Theory");

	private static bool IsStaticSetValue(InvocationExpressionSyntax invocation)
	{
		if (invocation.Expression is not MemberAccessExpressionSyntax access
			|| !access.Name.Identifier.ValueText.StartsWith("SetValue", StringComparison.Ordinal))
		{
			return false;
		}

		var firstPositional = true;
		foreach (var argument in invocation.ArgumentList.Arguments)
		{
			var isTarget = argument.NameColon is { } nameColon
				? nameColon.Name.Identifier.ValueText == "obj"
				: firstPositional;
			if (argument.NameColon is null)
			{
				firstPositional = false;
			}

			if (isTarget)
			{
				return IsNullLiteral(argument.Expression);
			}
		}

		return false;
	}

	private static bool IsNullLiteral(ExpressionSyntax expression) =>
		expression.IsKind(SyntaxKind.NullLiteralExpression)
		|| (expression is PostfixUnaryExpressionSyntax postfix
			&& postfix.IsKind(SyntaxKind.SuppressNullableWarningExpression)
			&& postfix.Operand.IsKind(SyntaxKind.NullLiteralExpression));

	private static IEnumerable<string> EnumerateTestSources() =>
		Directory.EnumerateFiles(RepositoryPaths.File(TestProjectDir), "*.cs", SearchOption.AllDirectories)
			.Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
				&& !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

	private static string Relative(string path) => Path.GetRelativePath(RepositoryPaths.Root, path);
}
