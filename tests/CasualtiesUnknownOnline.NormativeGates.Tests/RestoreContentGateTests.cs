using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// The restore's refused-content policy (user ruling 2026-10-23): a content item its container will not take is
/// DROPPED INTO THE WORLD, never destroyed and never pushed into a slot. Destroying it is not a local act — the
/// item's own end is reported at the end of the frame and the host stores the id terminal — so a legitimate item
/// vanished because a container's local rules disagreed with the snapshot (the shape batch <c>20261021-a</c> lost a
/// handed-over toolbox to, one call site over).
///
/// <para>
/// A live <c>Container</c>/<c>Item</c> cannot exist in this test host, so the rule is read from the policy's own
/// syntax: the method must exist, it must contain no <c>Object.Destroy</c> call, the composition must bind the drop
/// seam, and the drop implementation must report the creation (an unreported world object is swept away by the next
/// world-item keyframe, which would lose the item just as surely).
/// </para>
/// </summary>
public class RestoreContentGateTests
{
	private const string CodecFile = "src/CasualtiesUnknownOnline.GameAdapter/Items/ItemStateCodec.cs";

	private const string DomainsFile = "src/CasualtiesUnknownOnline.GameAdapter/GameAdapterDomains.cs";

	private const string WorldItemCreationFile = "src/CasualtiesUnknownOnline.GameAdapter/Items/WorldItemCreation.cs";

	[Fact]
	public void TheRefusedContentPolicy_DropsTheChildInsteadOfDestroyingIt()
	{
		var root = CSharpSyntaxTree
			.ParseText(RepositoryPaths.ReadText(CodecFile), new CSharpParseOptions(LanguageVersion.Preview))
			.GetRoot();
		var policy = FindRefusedContentPolicy(root);

		Assert.True(
			policy is not null,
			$"{CodecFile} declares no refused-content policy (`RestoreContentDropping`) — the restore paths call a method this gate cannot find, and the rule would pass by finding nothing");

		Assert.False(
			DestroysTheChild(policy!),
			$"{CodecFile}'s refused-content policy destroys the child the container refused — the user's 2026-10-23 ruling is that it falls to the ground: report it as a world item (the same creation report the give ladder's drop rung makes), never destroy an id-carrying object");
	}

	[Fact]
	public void TheComposition_BindsTheRefusedContentDrop()
	{
		var root = CSharpSyntaxTree
			.ParseText(RepositoryPaths.ReadText(DomainsFile), new CSharpParseOptions(LanguageVersion.Preview))
			.GetRoot();

		Assert.Contains(
			root.DescendantNodes().OfType<InvocationExpressionSyntax>(),
			invocation => invocation.Expression.ToString() == "ItemStateCodec.BindRefusedContentDrop");
	}

	[Fact]
	public void TheDrop_ReportsTheCreation()
	{
		var root = CSharpSyntaxTree
			.ParseText(RepositoryPaths.ReadText(WorldItemCreationFile), new CSharpParseOptions(LanguageVersion.Preview))
			.GetRoot();
		var drop = root.DescendantNodes()
			.OfType<MethodDeclarationSyntax>()
			.FirstOrDefault(method => method.Identifier.ValueText == "DropAsWorldItem");

		Assert.True(drop is not null, $"{WorldItemCreationFile} declares no `DropAsWorldItem` — the bound seam has no implementation this gate can read");
		Assert.Contains(
			drop!.DescendantNodes().OfType<InvocationExpressionSyntax>(),
			invocation => invocation.Expression.ToString() == "_items.SendItemSpawned");
	}

	[Theory]
	// The shape this gate was written for: the bulk restore destroyed the child it could not place.
	[InlineData("internal static void RestoreContentDiscarding(Item containerItem, Container container, CharacterItemMsg childData) { if (RestoreContent(containerItem, container, childData) is { Loaded: false, Item: { } discarded }) { Object.Destroy(discarded.gameObject); } }", true)]
	// The ruled shape: the child is handed to the world drop.
	[InlineData("internal static void RestoreContentDropping(Item containerItem, Container container, CharacterItemMsg childData) { if (RestoreContent(containerItem, container, childData) is { Loaded: false, Item: { } refused }) { drop(refused, \"Restore\"); } }", false)]
	// A destroying method that is not the policy is not what this gate reads.
	[InlineData("internal static void RestoreContentSomethingElse(Item item) { Object.Destroy(item.gameObject); }", false)]
	public void TheDestroyMatcher_ReadsThePolicyItself(string snippet, bool expected)
	{
		var root = CSharpSyntaxTree
			.ParseText($"static class C {{ {snippet} }}", new CSharpParseOptions(LanguageVersion.Preview))
			.GetRoot();
		var policy = FindRefusedContentPolicy(root);

		Assert.Equal(expected, policy is not null && DestroysTheChild(policy));
	}

	private static MethodDeclarationSyntax? FindRefusedContentPolicy(SyntaxNode root) =>
		root.DescendantNodes()
			.OfType<MethodDeclarationSyntax>()
			.FirstOrDefault(method => method.Identifier.ValueText is "RestoreContentDropping" or "RestoreContentDiscarding");

	private static bool DestroysTheChild(MethodDeclarationSyntax policy) =>
		policy.DescendantNodes()
			.OfType<InvocationExpressionSyntax>()
			.Any(invocation => invocation.Expression.ToString().StartsWith("Object.Destroy", StringComparison.Ordinal));
}
