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
/// The declared-behaviour gate. A mod can declare, through the content API, that
/// its item is usable and that its liquid can be applied to a limb or injected —
/// but that API carries no behaviour function for either, and the game's own call
/// sites do not null-check what they run: <c>Body.UseItem</c> and
/// <c>Body.UseItemInHand</c> run <c>ItemInfo.useAction</c> behind the item's own
/// <c>usable</c> flag (<c>Body.UseItemInHand</c> additionally requires
/// <c>usableWithLMB</c>; <c>reversing/Assembly-CSharp/Assembly-CSharp/Body.cs:2479</c>
/// and <c>:2454</c>), and <c>WaterContainerItem.Drink</c> / <c>ApplyToLimb</c> /
/// <c>Inject</c> run a liquid's <c>onDrink</c> / <c>onHealthUse</c>
/// (<c>WaterContainerItem.cs:210</c>, <c>:230</c>, <c>:256</c>) — the drink one with
/// no flag in front of it at all.
///
/// <para>
/// So the rule this gate states is about what the framework CONSTRUCTS: every
/// <c>ItemInfo</c> and every <c>LiquidType</c> built under <c>src/</c> must be
/// built by a member that also assigns the delegate the game runs, which is what
/// keeps a declaration from becoming a null call inside the game's own path. The
/// scan surface is every <c>.cs</c> file under <c>src/</c>, derived from the tree
/// rather than a list, with a census floor so a scan that read nothing fails.
/// </para>
///
/// <para>
/// <c>ItemInfo.useLimbAction</c> is deliberately not a row: no content DTO can set
/// <c>usableOnLimb</c>, so no construction this framework performs can carry a limb
/// action. That gap is the ceiling ticket's own Part 3.A entry, and this table grows
/// a row the day a provider builds an item that has one.
/// </para>
/// </summary>
public class ContentBehaviourDelegateGateTests
{
	/// <summary>Each game type the framework constructs, and the delegate members its builder must assign.</summary>
	private static readonly (string Type, string[] Delegates)[] Required =
	[
		("ItemInfo", ["useAction"]),
		("LiquidType", ["onDrink", "onHealthUse"])
	];

	/// <summary>The framework constructs both of these today; a scan that read fewer has broken.</summary>
	private const int ConstructionFloor = 2;

	[Fact]
	public void FrameworkConstructedGameContent_CarriesTheDelegateTheGameRunsWithoutANullCheck()
	{
		var constructions = SourceScan.SourceFiles(RepositoryPaths.File("src"))
			.SelectMany(path => Constructions(
				Path.GetRelativePath(RepositoryPaths.Root, path).Replace('\\', '/'),
				File.ReadAllText(path)))
			.ToArray();

		Assert.True(constructions.Length >= ConstructionFloor,
			$"the scan read {constructions.Length} construction(s) under src/");

		foreach (var (type, _) in Required)
		{
			Assert.True(constructions.Any(construction => string.Equals(construction.Type, type, StringComparison.Ordinal)),
				$"no construction of {type} was found under src/ — the scan surface broke, it did not improve");
		}

		Assert.Empty(constructions
			.Where(construction => construction.Missing.Count > 0)
			.Select(construction =>
				$"{construction.File}: `new {construction.Type}` in {construction.Member} never assigns {string.Join(" / ", construction.Missing)}"));
	}

	[Theory]
	[InlineData("class C { void M() { var info = new ItemInfo(); info.useAction = Make(); } }", "")]
	[InlineData("class C { void M() { var info = new ItemInfo { useAction = Make() }; } }", "")]
	[InlineData("class C { void M() { var info = new ItemInfo(); } }", "useAction")]
	[InlineData("class C { void M() { var info = new ItemInfo(); } void N(ItemInfo info) { info.useAction = Make(); } }", "useAction")]
	[InlineData("class C { void M(ModItemDefinition definition) { var info = new ItemInfo(); if (definition.Tool is { } tool) { info.useAction = Make(); } } }", "useAction")]
	[InlineData("class C { void M(ModItemDefinition definition) { var info = new ItemInfo(); if (definition.Tool is { } tool) { info.useAction = Make(); } if (info.usable) { info.useAction = Make(); } } }", "")]
	[InlineData("class C { void M() { var liquid = new LiquidType { onDrink = Make() }; } }", "onHealthUse")]
	[InlineData("class C { void M() { ItemInfo info = new() { }; } }", "useAction")]
	[InlineData("class C { void M() { ItemInfo info = new() { useAction = Make() }; } }", "")]
	[InlineData("class C { void M() { var snapshot = new ItemInfoSnapshot(); } }", "")]
	public void TheConstructionMatcher_ReadsTheDelegateAssignedInTheSameMember(string body, string expected) =>
		Assert.Equal(
			expected,
			string.Join(",", Missing("sample.cs", body)));

	/// <summary>
	/// Every construction of a delegate-bearing game type in one source file, with
	/// the members its BUILDING member never assigns. The type is matched exactly,
	/// so a differently named type (<c>ItemInfoSnapshot</c>) is not a construction
	/// of it; the assignment is resolved by member NAME (<c>x.useAction = …</c> and
	/// an initializer's <c>useAction = …</c> both count); and an assignment a
	/// definition SLICE's own branch gates counts for that slice only, never as the
	/// default a definition with no behaviour still needs.
	/// </summary>
	internal static List<Construction> Constructions(string file, string source)
	{
		var root = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview)).GetRoot();
		var constructions = new List<Construction>();
		foreach (var creation in root.DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>())
		{
			if (ConstructedType(creation) is not { } type || RequiredDelegates(type) is not { } required)
			{
				continue;
			}

			var member = creation.Ancestors().OfType<MemberDeclarationSyntax>().FirstOrDefault();
			var assigned = member?.DescendantNodes()
				.OfType<AssignmentExpressionSyntax>()
				.Where(assignment => assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
					&& !SliceConditional(assignment))
				.Select(assignment => Name(assignment.Left.ToString()))
				.ToHashSet(StringComparer.Ordinal);
			constructions.Add(new Construction(
				file,
				type,
				member is MethodDeclarationSyntax method ? method.Identifier.ValueText : "(type body)",
				[.. required.Where(delegateName => assigned is null || !assigned.Contains(delegateName))]));
		}

		return constructions;
	}

	/// <summary>
	/// True when the assignment sits inside a branch an AUTHORED SLICE of the mod
	/// definition gates — a conditional whose governing expression names
	/// <c>definition</c>. That assignment is the slice's own delegate (a Tool or Gun
	/// behaviour), not the default installation a definition which authors no
	/// behaviour at all still needs.
	/// </summary>
	internal static bool SliceConditional(SyntaxNode assignment) =>
		assignment.Ancestors().Any(ancestor => ancestor switch
		{
			IfStatementSyntax statement => NamesDefinition(statement.Condition),
			SwitchStatementSyntax statement => NamesDefinition(statement.Expression),
			ConditionalExpressionSyntax expression => NamesDefinition(expression.Condition),
			_ => false
		});

	private static bool NamesDefinition(SyntaxNode expression) =>
		expression.DescendantNodesAndSelf()
			.OfType<IdentifierNameSyntax>()
			.Any(identifier => string.Equals(identifier.Identifier.ValueText, "definition", StringComparison.Ordinal));

	/// <summary>The delegate members a construction of <paramref name="type"/> must carry, or null when the type is not one of them.</summary>
	internal static string[]? RequiredDelegates(string type) =>
		Required
			.Where(entry => string.Equals(entry.Type, type, StringComparison.Ordinal))
			.Select(entry => entry.Delegates)
			.FirstOrDefault();

	/// <summary>
	/// The type a construction names: the creation's own type name, or — for a
	/// target-typed <c>new()</c>, which names none — the declared type of the
	/// variable it initialises. Null when neither is available, which the caller
	/// reads as "not one of the types this gate is about".
	/// </summary>
	internal static string? ConstructedType(BaseObjectCreationExpressionSyntax creation) =>
		creation is ObjectCreationExpressionSyntax named
			? named.Type.ToString()
			: creation.Ancestors().OfType<VariableDeclarationSyntax>().FirstOrDefault()?.Type.ToString();

	/// <summary>The member name of an assignment target: the last segment of <c>info.useAction</c>, or the whole name of an initializer's <c>onDrink</c>.</summary>
	private static string Name(string target) =>
		target[(target.LastIndexOf('.') + 1)..];

	private static List<string> Missing(string file, string source) =>
		[.. Constructions(file, source).SelectMany(construction => construction.Missing)];

	/// <summary>One construction of a delegate-bearing game type, and what its building member failed to install.</summary>
	internal sealed record Construction(string File, string Type, string Member, IReadOnlyList<string> Missing);
}
