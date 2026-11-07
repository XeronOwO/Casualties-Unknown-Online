using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// The item-grant gate (ticket <c>backlog/todo/give-creates-empty-liquid-containers.md</c>, batch `20261107-o`).
///
/// <para>
/// A console grant (the <c>/give</c> and <c>/summon</c> verbs) used to copy the STATE OF THE PREFAB ASSET:
/// <c>ItemGrantSource.TryCreate</c> resolved the definition's <c>Item</c> component (the asset — its
/// <c>Awake</c> never ran, so the water container's <c>stack</c> was null and its serialized
/// <c>condition</c> was 0) and captured that. The game's own creation path (<c>/spawn</c> →
/// <c>Utils.Create</c> + <c>SetCondition(1f)</c>) instantiates the template first, so the instance's
/// <c>Awake</c> fills the default contents — and every item a player receives must be made the same way, or
/// the wound view refuses it and the guest disagrees about the same item.
/// </para>
///
/// <para>
/// The rule this gate encodes: the grant captures a GAME-CREATED INSTANCE, sets the game's own default
/// condition on it, and never captures the template parameter; the only remaining template capture is
/// <c>ItemStateDocument.DefinitionSchema</c>, which derives a schema shape and never a value.
/// </para>
/// </summary>
public class ItemGrantGateTests
{
	private const string ItemGrantSourceFile = "src/CasualtiesUnknownOnline.GameAdapter/Items/ItemGrantSource.cs";
	private const string ItemStateDocumentFile = "src/CasualtiesUnknownOnline.GameAdapter/Items/ItemStateDocument.cs";

	[Fact]
	public void TheGrant_CapturesAGameCreatedInstance_NotTheTemplateAsset()
	{
		var body = RequireMethodBody(ItemGrantSourceFile, "ItemGrantSource", "TryCreate");

		Assert.True(
			body.Contains("InstantiateResource(", StringComparison.Ordinal),
			$"{ItemGrantSourceFile}: the grant must instantiate the definition's template — a prefab asset's Awake never ran, so its condition is 0 and its container's stack is null, while the game's own /spawn creates an instance first");
		Assert.True(
			body.Contains("SetCondition(", StringComparison.Ordinal),
			$"{ItemGrantSourceFile}: the created instance must take the game's own default condition (the /spawn verb sets 1f) before it is captured");
		Assert.True(
			body.Contains("CaptureItem(created,", StringComparison.Ordinal),
			$"{ItemGrantSourceFile}: the capture must read the created instance, not the template parameter");
		Assert.DoesNotContain("CaptureItem(template", body);
	}

	[Fact]
	public void TheTemplateCaptureCensus_StaysAtTheSchemaPath()
	{
		var grant = RepositoryPaths.ReadText(ItemGrantSourceFile);
		Assert.DoesNotContain("CaptureItem(template", grant);

		var document = RepositoryPaths.ReadText(ItemStateDocumentFile);
		Assert.Contains("CaptureItem(template, slotIndex: -1)", document);
	}

	/// <summary>The body text of a method declared in one file, or a failed assertion naming the stale scan surface.</summary>
	private static string RequireMethodBody(string file, string typeName, string methodName)
	{
		var type = Parse(RepositoryPaths.ReadText(file)).DescendantNodes()
			.OfType<TypeDeclarationSyntax>()
			.FirstOrDefault(declaration => string.Equals(declaration.Identifier.ValueText, typeName, StringComparison.Ordinal));
		Assert.True(type is not null, $"{file} no longer declares `{typeName}` — this gate's scan surface is stale, not clean");

		var method = type!.Members.OfType<MethodDeclarationSyntax>()
			.FirstOrDefault(candidate => string.Equals(candidate.Identifier.ValueText, methodName, StringComparison.Ordinal));
		Assert.True(method is not null, $"{file}: `{typeName}` no longer declares `{methodName}` — this gate's scan surface is stale, not clean");

		return method!.Body is not null ? method.Body.ToString() : method.ExpressionBody?.ToString() ?? string.Empty;
	}

	private static SyntaxNode Parse(string source) =>
		CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview)).GetRoot();
}
