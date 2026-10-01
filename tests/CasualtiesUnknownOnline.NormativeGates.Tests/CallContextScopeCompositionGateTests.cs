using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// The call-identity composition gate (ticket
/// <c>backlog/todo/unhooked-damage-block-callers.md</c> row 4 — batch
/// `20261002-c` falsified "a remote apply stays silent").
///
/// <para>
/// <c>CallContext</c> states WHO is mutating the scene: the guard sites ask it
/// whether the mutation is a REMOTE application and stay silent when it is. The
/// stack nests — a remote apply runs the game's own damage roll and the roll
/// pushes <c>DamageBlockOrigin</c>, an inventory load pushes
/// <c>InternalReorder</c>, a craft pushes <c>Craft</c> — and
/// <c>CallContext.Current</c> answers the INNERMOST origin. A guard that asks
/// <c>Current == RemoteApply</c> therefore stops seeing the remote apply as soon
/// as the applied code opens a sub-scope: the row-4 leak is exactly the damage
/// patch's <c>DamageBlockOrigin</c> hiding the caller's <c>RemoteApply</c> from
/// the <c>SetBlock</c> hook, so the receiving side's presentation write was
/// reported back to the host as a local player break.
/// </para>
///
/// <para>
/// The rule this gate encodes: MUTATION ATTRIBUTION uses the compositional query
/// (<c>CallContext.IsWithin(CallContext.Origin.RemoteApply)</c>, a chain scan);
/// <c>CallContext.Current</c> stays the INNERMOST origin for the CLASSIFICATION
/// guards (which flavour a scope names). A guard that asks the innermost origin
/// for remote-apply identity cannot survive the nested scope it exists for; the
/// source shape is banned here, and the query's own semantics are pinned
/// behaviourally by
/// <c>CasualtiesUnknownOnline.Tests/Patching/CallContextCompositionTests</c>.
/// </para>
///
/// <para>
/// Reach, stated rather than implied: the masking-prone census reads expressions
/// under <c>src/</c> by Roslyn (mentions in comments and string literals are not
/// expressions and are not flagged); it names the four comparison forms that
/// exist today and cannot see a remote-apply test expressed some other way (a
/// helper method, a switch arm). The attribution census counts
/// <c>IsWithin(RemoteApply)</c> INVOCATIONS under <c>src/</c> and holds a floor,
/// so an emptied pin fails loudly.
/// </para>
/// </summary>
public class CallContextScopeCompositionGateTests
{
	private const string CallContextFile = "src/CasualtiesUnknownOnline.GameAdapter/CallContext.cs";

	/// <summary>The compositional attribution query every remote-apply guard use.</summary>
	private const string AttributionQuery = "CallContext.IsWithin(CallContext.Origin.RemoteApply)";

	/// <summary>The census floor: the fix converts every remote-apply guard site, so an emptied pin must fail rather than check nothing.</summary>
	private const int MinimumAttributionSites = 12;

	/// <summary>The four source forms that answer remote-apply identity from the INNERMOST origin — every one of them is masked by a nested scope.</summary>
	private static readonly HashSet<string> MaskingProneForms =
	[
		"CallContext.Current == CallContext.Origin.RemoteApply",
		"CallContext.Current != CallContext.Origin.RemoteApply",
		"CallContext.Current is CallContext.Origin.RemoteApply",
		"CallContext.Current is not CallContext.Origin.RemoteApply",
	];

	[Fact]
	public void CallContext_DeclaresACompositionalAttributionQuery()
	{
		Assert.True(
			DeclaresCompositionalQuery(RepositoryPaths.ReadText(CallContextFile)),
			$"{CallContextFile} must declare `internal static bool IsWithin(Origin origin)` — the chain query the remote-apply guards use instead of the innermost origin");
	}

	[Fact]
	public void NoProductionSource_TestsRemoteApplyIdentityThroughTheInnermostOrigin()
	{
		var offenders = new List<string>();
		foreach (var file in SourceFiles())
		{
			foreach (var expression in MaskingProneExpressions(File.ReadAllText(file)))
			{
				offenders.Add($"{Relative(file)}: {expression}");
			}
		}

		Assert.True(
			offenders.Count == 0,
			"a nested scope hides the caller's RemoteApply from `CallContext.Current`; remote-apply attribution must use "
			+ $"`{AttributionQuery}` (see CallContext.cs):"
			+ Environment.NewLine
			+ string.Join(Environment.NewLine, offenders));
	}

	[Fact]
	public void TheAttributionQuery_IsUsedAtEveryGuardSite()
	{
		var census = SourceFiles().Sum(file => CompositionalQueryCensus(File.ReadAllText(file)));
		Assert.True(
			census >= MinimumAttributionSites,
			$"found {census} `{AttributionQuery}` invocation(s) under src/ — the census floor is {MinimumAttributionSites}, so the matcher (or the guard sites) changed shape");
	}

	[Theory]
	[InlineData("var x = CallContext.Current == CallContext.Origin.RemoteApply;", 1)]
	[InlineData("var x = CallContext.Current != CallContext.Origin.RemoteApply;", 1)]
	[InlineData("var x = CallContext.Current is CallContext.Origin.RemoteApply;", 1)]
	[InlineData("if (CallContext.Current is not CallContext.Origin.RemoteApply) { return; }", 1)]
	[InlineData("// CallContext.Current == CallContext.Origin.RemoteApply", 0)]
	[InlineData("\"CallContext.Current == CallContext.Origin.RemoteApply\"", 0)]
	[InlineData("var x = CallContext.Current == CallContext.Origin.DamageBlockOrigin;", 0)]
	[InlineData("var x = CallContext.IsWithin(CallContext.Origin.RemoteApply);", 0)]
	public void TheMaskingProneMatcher_ReadsExpressionsAndIgnoresMentions(string source, int expected) =>
		Assert.Equal(expected, MaskingProneExpressions(source).Count());

	[Theory]
	[InlineData("var x = CallContext.IsWithin(CallContext.Origin.RemoteApply);", 1)]
	[InlineData("// CallContext.IsWithin(CallContext.Origin.RemoteApply)", 0)]
	[InlineData("var x = CallContext.IsWithin(CallContext.Origin.DamageBlockOrigin);", 0)]
	[InlineData("var x = CallContext.IsWithin(CallContext.Origin.RemoteApply) == false;", 1)]
	public void TheAttributionCensus_CountsInvocationsAndIgnoresMentions(string source, int expected) =>
		Assert.Equal(expected, CompositionalQueryCensus(source));

	private static bool DeclaresCompositionalQuery(string source)
	{
		var type = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview))
			.GetRoot()
			.DescendantNodes()
			.OfType<TypeDeclarationSyntax>()
			.FirstOrDefault(declaration => string.Equals(declaration.Identifier.ValueText, "CallContext", StringComparison.Ordinal));
		if (type is null)
		{
			return false;
		}

		return type.Members
			.OfType<MethodDeclarationSyntax>()
			.Any(method =>
				string.Equals(method.Identifier.ValueText, "IsWithin", StringComparison.Ordinal)
				&& method.Modifiers.Any(token => token.RawKind == (int)SyntaxKind.StaticKeyword)
				&& string.Equals(method.ReturnType.ToString(), "bool", StringComparison.Ordinal)
				&& method.ParameterList.Parameters.Count == 1
				&& string.Equals(method.ParameterList.Parameters[0].Type?.ToString(), "Origin", StringComparison.Ordinal));
	}

	private static IEnumerable<string> MaskingProneExpressions(string source) =>
		CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview))
			.GetRoot()
			.DescendantNodes()
			.OfType<ExpressionSyntax>()
			.Select(expression => expression.ToString())
			.Where(MaskingProneForms.Contains);

	private static int CompositionalQueryCensus(string source) =>
		CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview))
			.GetRoot()
			.DescendantNodes()
			.OfType<InvocationExpressionSyntax>()
			.Count(invocation => string.Equals(invocation.ToString(), AttributionQuery, StringComparison.Ordinal));

	private static IEnumerable<string> SourceFiles() =>
		Directory.EnumerateFiles(RepositoryPaths.File("src"), "*.cs", SearchOption.AllDirectories);

	private static string Relative(string file) => file.Substring(RepositoryPaths.Root.Length + 1);
}
