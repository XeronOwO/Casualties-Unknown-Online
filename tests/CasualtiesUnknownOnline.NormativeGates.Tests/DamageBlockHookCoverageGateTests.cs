using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// The block-damage hook-coverage gate (ticket
/// <c>backlog/todo/unhooked-damage-block-callers</c>): CUO must hook the
/// <c>WorldGeneration.DamageBlock</c> overload that EVERY native damage roll
/// enters, and the report that hook produces must carry the CELL.
///
/// <para>
/// Why this is a gate rather than a unit test: <c>WorldGeneration</c> declares
/// two overloads — the body that rolls the damage and writes the cell
/// (<c>DamageBlock(Vector2Int, float, bool, bool, bool)</c>, WorldGeneration.cs:711)
/// and a converting forwarder that only calls it
/// (<c>DamageBlock(Vector2, float, bool, bool)</c>, WorldGeneration.cs:851-854).
/// Three of the five native call sites (Body.cs:1929, Limb.cs:384,
/// TurretScript.cs:144) enter through the forwarder, but the footstep crush
/// (Body.cs:2709) and the spider burrow (SpiderHandler.cs:218) call the body
/// directly, so an anchor on the forwarder leaves those two reported NOWHERE:
/// damage applied and sounds played on the acting side, and nothing on the
/// peers. That is a routing fact — no pure function in this suite can observe
/// which overload a Harmony attribute names — and the patch itself needs the
/// running game, so the pin is read from SOURCE by Roslyn and runs in the fast
/// suite. What a player HEARS is the user's dual-client acceptance, never this
/// gate.
/// </para>
///
/// <para>
/// Reach, stated rather than implied: the anchor census reads the
/// <c>[HarmonyPatch]</c> attribute's argument-type list, and the report census
/// reads the first parameter of each <c>OnBlockDamaged</c> declaration in the
/// three files that carry that seam. An anchor expressed some other way (a stack
/// of <c>HarmonyPatch</c> attributes, a manual <c>PatchProcessor</c> call) is
/// out of reach — neither shape exists in this tree, and the counts asserted
/// here make a silent disappearance fail loudly instead of passing by checking
/// nothing. The Harmony parameter-name matching, and the fact that the anchored
/// method exists in the game assembly at all, are the patch-contract tests'
/// reach (<c>PatchContractTests</c>, the game-assembly host), not this gate's;
/// the report census counts declarations file-wide, which the
/// <c>Count == 1</c> assertion keeps safe, and the seam's own member census is
/// <c>PatchBridgePortShapeGateTests</c>'.
/// </para>
/// </summary>
public class DamageBlockHookCoverageGateTests
{
	private const string AdapterDir = "src/CasualtiesUnknownOnline.GameAdapter/";

	private const string PatchFile = AdapterDir + "Patches/WorldGenerationDamageBlockPatch.cs";

	private const string AggregateFile = AdapterDir + "IPatchBridge.cs";

	private const string BridgeFile = AdapterDir + "GameAdapterBridge.cs";

	private const string BlockBreakFile = AdapterDir + "World/BlockBreakSync.cs";

	/// <summary>
	/// The choke point: the body overload every native <c>DamageBlock</c> caller
	/// enters, including the phase after another overload converted a world
	/// position. The forwarder's own parameter list (the same shape with
	/// <c>Vector2</c> and without <c>ignoreLoot</c>) is the one shape this pin
	/// exists to reject.
	/// </summary>
	private static readonly string[] ChokePointOverload = ["Vector2Int", "float", "bool", "bool", "bool"];

	/// <summary>The pinned overload's floor — a pin emptied alongside its source would otherwise pass by checking nothing.</summary>
	private const int MinimumAnchoredParameters = 4;

	[Fact]
	public void ThePatch_AnchorsTheOverloadEveryNativeCallerEnters()
	{
		Assert.True(
			ChokePointOverload.Length >= MinimumAnchoredParameters,
			$"the pinned overload census holds only {ChokePointOverload.Length} type(s) — the pin was emptied, not the anchor");

		var attributes = HarmonyPatchAttributes(RepositoryPaths.ReadText(PatchFile));
		Assert.True(
			attributes.Length == 1,
			$"{PatchFile} declares {attributes.Length} [HarmonyPatch] attribute(s) — this gate reads exactly one target per patch class; a second one needs its own review");

		var anchored = ArgumentTypesOf(attributes[0]);
		Assert.True(
			AnchorsTheChokePoint(anchored),
			$"{PatchFile} anchors DamageBlock({string.Join(", ", anchored)}) — every native damage roll enters "
			+ $"DamageBlock({string.Join(", ", ChokePointOverload)}) (the Vector2 overload only converts and forwards, WorldGeneration.cs:851-854), "
			+ "so anchoring the forwarder reports nothing for the footstep crush (Body.cs:2709) and the spider burrow (SpiderHandler.cs:218)");

		Assert.True(
			string.Equals("WorldGeneration", DeclaringTypeOf(attributes[0]), StringComparison.Ordinal),
			$"{PatchFile} anchors {DeclaringTypeOf(attributes[0])}.DamageBlock — the block-damage roll lives on WorldGeneration, and a same-shaped method on another type would be a different roll entirely");
	}

	/// <summary>
	/// The report is cell-keyed at every one of the seam's three declarations: the
	/// patch no longer converts (the body overload hands it the cell), and the
	/// wire already carries the cell pair — a world position reappearing here would
	/// mean a conversion at the seam again, which is where the two formulas could
	/// drift apart.
	/// </summary>
	[Fact]
	public void TheReport_CarriesTheCellRatherThanAWorldPosition()
	{
		foreach (var file in new[] { AggregateFile, BridgeFile, BlockBreakFile })
		{
			var declarations = FirstParameterTypes(file, "OnBlockDamaged");
			Assert.True(
				declarations.Count == 1,
				$"{file} declares OnBlockDamaged {declarations.Count} time(s) — this gate reads exactly one declaration per file");
			Assert.True(
				string.Equals("Vector2Int", declarations[0], StringComparison.Ordinal),
				$"{file} takes {declarations[0]} where the report's cell has to travel — the block-damage report is cell-keyed");
		}
	}

	[Theory]
	[InlineData("[HarmonyPatch(typeof(WorldGeneration), \"DamageBlock\", [typeof(Vector2Int), typeof(float), typeof(bool), typeof(bool), typeof(bool)])]\ninternal static class P { }", "Vector2Int,float,bool,bool,bool")]
	[InlineData("[HarmonyPatch(typeof(Sound), \"Play\", new[] { typeof(string), typeof(Vector2) })]\ninternal static class P { }", "string,Vector2")]
	[InlineData("[HarmonyPatch(typeof(WorldGeneration), \"DamageBlock\")]\ninternal static class P { }", "")]
	[InlineData("/// a doc mention: [HarmonyPatch(typeof(WorldGeneration), \"DamageBlock\", [typeof(Vector2)])]\n[HarmonyPatch(typeof(WorldGeneration), \"DamageBlock\", [typeof(Vector2Int)])]\ninternal static class P { }", "Vector2Int")]
	public void TheAnchorCensus_ReadsTheAttributeArgumentsAndIgnoresMentions(string source, string expected) =>
		Assert.Equal(expected, string.Join(",", ArgumentTypes(source)), StringComparer.Ordinal);

	[Theory]
	[InlineData("Vector2Int,float,bool,bool,bool", true)]
	[InlineData("Vector2,float,bool,bool", false)]
	[InlineData("Vector2Int,float,bool,bool", false)]
	[InlineData("", false)]
	public void TheChokePointPredicate_AcceptsOnlyTheBodyOverload(string argumentTypes, bool expected) =>
		Assert.Equal(expected, AnchorsTheChokePoint(Split(argumentTypes)));

	/// <summary>
	/// The anchor is a TARGET as well as a signature: the declaring type is read
	/// from the same attribute, so a same-shaped method on another type — which
	/// would hook a roll this gate knows nothing about — fails here rather than
	/// downstream.
	/// </summary>
	[Theory]
	[InlineData("[HarmonyPatch(typeof(WorldGeneration), \"DamageBlock\", [typeof(Vector2Int), typeof(float), typeof(bool), typeof(bool), typeof(bool)])]\ninternal static class P { }", "WorldGeneration", true)]
	[InlineData("[HarmonyPatch(typeof(Sound), \"DamageBlock\", [typeof(Vector2Int), typeof(float), typeof(bool), typeof(bool), typeof(bool)])]\ninternal static class P { }", "Sound", true)]
	[InlineData("[HarmonyPatch(typeof(WorldGeneration), \"DamageBlock\", [typeof(Vector2), typeof(float), typeof(bool), typeof(bool)])]\ninternal static class P { }", "WorldGeneration", false)]
	public void TheAnchor_NamesTheWorldGenerationTargetAndTheBodyOverload(string source, string expectedType, bool expectedAnchor)
	{
		var attribute = HarmonyPatchAttributes(source).First();
		Assert.Equal(expectedType, DeclaringTypeOf(attribute), StringComparer.Ordinal);
		Assert.Equal(expectedAnchor, AnchorsTheChokePoint(ArgumentTypesOf(attribute)));
	}

	/// <summary>Every <c>[HarmonyPatch]</c> attribute the source declares — comments and doc mentions are not nodes, so they cannot be counted.</summary>
	private static AttributeSyntax[] HarmonyPatchAttributes(string source) =>
		[.. CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview))
			.GetRoot()
			.DescendantNodes()
			.OfType<AttributeSyntax>()
			.Where(attribute => string.Equals(SimpleName(attribute.Name), "HarmonyPatch", StringComparison.Ordinal))];

	/// <summary>The attribute's argument-TYPE list (the third argument's elements), in declaration order; an attribute without one pins no overload.</summary>
	private static string[] ArgumentTypes(string source)
	{
		var attribute = HarmonyPatchAttributes(source).FirstOrDefault();
		return attribute is null ? [] : ArgumentTypesOf(attribute);
	}

	private static string[] ArgumentTypesOf(AttributeSyntax attribute)
	{
		var list = attribute.ArgumentList?.Arguments
			.Select(argument => argument.Expression)
			.FirstOrDefault(IsTypeList);

		return list is null
			? []
			: [.. TypeListElements(list).OfType<TypeOfExpressionSyntax>().Select(type => SimpleTypeName(type.Type))];
	}

	/// <summary>The declaring type the attribute names — its first argument; empty when the attribute names none.</summary>
	private static string DeclaringTypeOf(AttributeSyntax attribute) =>
		attribute.ArgumentList?.Arguments
			.Select(argument => argument.Expression)
			.OfType<TypeOfExpressionSyntax>()
			.Select(type => SimpleTypeName(type.Type))
			.FirstOrDefault() ?? string.Empty;

	private static bool IsTypeList(ExpressionSyntax expression) =>
		expression is CollectionExpressionSyntax or ImplicitArrayCreationExpressionSyntax or ArrayCreationExpressionSyntax;

	private static IReadOnlyList<ExpressionSyntax> TypeListElements(ExpressionSyntax expression) => expression switch
	{
		CollectionExpressionSyntax collection => [.. collection.Elements.OfType<ExpressionElementSyntax>().Select(element => element.Expression)],
		ImplicitArrayCreationExpressionSyntax implicitArray => [.. implicitArray.Initializer.Expressions],
		ArrayCreationExpressionSyntax array => array.Initializer is null ? [] : [.. array.Initializer.Expressions],
		_ => [],
	};

	/// <summary>The first parameter type of every declaration of one method name, in the file's single type.</summary>
	private static IReadOnlyList<string> FirstParameterTypes(string relativePath, string methodName) =>
		[.. CSharpSyntaxTree.ParseText(RepositoryPaths.ReadText(relativePath), new CSharpParseOptions(LanguageVersion.Preview))
			.GetRoot()
			.DescendantNodes()
			.OfType<MethodDeclarationSyntax>()
			.Where(method => string.Equals(method.Identifier.ValueText, methodName, StringComparison.Ordinal))
			.Select(method => method.ParameterList.Parameters.Count == 0 ? string.Empty : SimpleTypeName(method.ParameterList.Parameters[0].Type))];

	private static bool AnchorsTheChokePoint(IReadOnlyList<string> argumentTypes) =>
		argumentTypes.SequenceEqual(ChokePointOverload, StringComparer.Ordinal);

	private static string[] Split(string argumentTypes) =>
		argumentTypes.Length == 0 ? [] : argumentTypes.Split(',');

	/// <summary>A type's simple name — the spelling the pins above compare against.</summary>
	private static string SimpleTypeName(TypeSyntax? type) => type switch
	{
		GenericNameSyntax generic => generic.Identifier.ValueText,
		QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
		IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
		_ => type?.ToString() ?? string.Empty,
	};

	private static string SimpleName(NameSyntax name) => name switch
	{
		QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
		_ => name.ToString(),
	};
}
