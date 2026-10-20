using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// The cross-player TOPICAL chain's entry pin: a topical application is the WOUND
/// VIEW's action — its native call site is <c>PlayerCamera.ApplyWoundItem</c>'s
/// <c>WaterContainerItem.ApplyToLimb(this.selectedLimb, 100f)</c> — so the limb the
/// operator picked IS the gesture, and the world drag (the inventory use: eat, drink,
/// wear) must not carry the family at all (decision 246). The host's half of the same
/// rule is a behavioural case in <c>ItemUseTests</c> (a topical request that names no
/// limb commits nothing); what no source gate can read is whether a live application
/// lands on that limb, which is the acceptance batch's row.
/// <para>
/// The entry split moved the family ORDER out of the adapter and into ONE Runtime
/// classifier, so the isolation scan reads the inventory entry's own arm there. That
/// is where the rule lives now, and a second order added back beside it — on the
/// operator's side or in the host's dispatch — is what the sibling cases below
/// refuse.
/// </para>
/// </summary>
public class TopicalChainGateTests
{
	private const string EligibilityFile = "src/CasualtiesUnknownOnline.GameAdapter/LocalUseItemEligibility.cs";

	private const string ApplyFile = "src/CasualtiesUnknownOnline.GameAdapter/NativeTopicalApply.cs";

	private const string ClassifierFile = "src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/ItemUseFamilyClassifier.cs";

	private const string HostChainFile = "src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/PlayerItemUseExecution.cs";

	private const string TreeFile = "src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/CarriedItemUseTree.cs";

	private static SyntaxNode Parse(string relativePath) =>
		CSharpSyntaxTree
			.ParseText(RepositoryPaths.ReadText(relativePath), new CSharpParseOptions(LanguageVersion.Preview))
			.GetRoot();

	/// <summary>Everything declared inside the named members, so a question can be asked of one gate rather than of a whole file.</summary>
	private static IEnumerable<SyntaxNode> Members(SyntaxNode root, string methodName) =>
		root.DescendantNodes()
			.OfType<MethodDeclarationSyntax>()
			.Where(method => method.Identifier.ValueText == methodName)
			.SelectMany(method => method.DescendantNodes());

	[Fact]
	public void TheWorldDragGate_DoesNotCarryTheTopicalFamily()
	{
		// The families the inventory use carries, by name: a drink and a meal ARE the
		// item's own use action and the wear placement is what the radial asks for; the
		// injection family is admitted only so the host can refuse it BY NAME, because
		// its native use action draws blood into the operator's bag.
		var inventoryFamilies = Members(Parse(ClassifierFile), "ClassifyInventory").ToList();
		Assert.True(inventoryFamilies.Count > 0, $"{ClassifierFile}: ClassifyInventory not found — the entry census read nothing");

		var names = inventoryFamilies
			.OfType<IdentifierNameSyntax>()
			.Select(identifier => identifier.Identifier.ValueText)
			.ToHashSet(StringComparer.Ordinal);
		Assert.Contains("Drink", names);
		Assert.Contains("SolidFood", names);
		Assert.Contains("Injection", names);

		// ...and the topical family is NOT among them: it belongs to the wound view, and
		// the limb it lands on is the one piece of information this gesture cannot carry.
		// Admitting it here is what put a topical dose on whichever limb the patient's
		// body happened to hold, which is the fallback decision 246 calls the defect. The
		// family's own predicate is rejected by name too, so re-adding it through
		// `IsTopicalRemoteItem` — the spelling the medical path uses — is caught as well.
		Assert.DoesNotContain(names, name => name is "Topical" or "TopicalAdmission" or "IsTopicalRemoteItem");

		// The adapter's side must be a DELEGATION to that one order, never a second
		// order of its own: the old bug lived here, in `FamilyOf` asking the topical
		// rule before the drink rule and shadowing the item's own `useAction`.
		var adapterGate = Members(Parse(EligibilityFile), "FamilyOf")
			.Concat(Members(Parse(EligibilityFile), "IsUseItem"))
			.ToList();
		Assert.True(adapterGate.Count > 0, $"{EligibilityFile}: FamilyOf/IsUseItem not found — the adapter half of the scan read nothing");
		Assert.Contains(
			adapterGate.OfType<MemberAccessExpressionSyntax>(),
			access => access.ToString() == "ItemUseFamilyClassifier.Classify");
		Assert.DoesNotContain(
			adapterGate.OfType<IdentifierNameSyntax>(),
			identifier => identifier.Identifier.ValueText is "Topical" or "TopicalAdmission" or "IsTopicalRemoteItem");
	}

	[Fact]
	public void TheHostChainAndTheAutoSelect_AskTheOneClassifier()
	{
		// The host's dispatch and its auto-select are the other two ends of the same
		// request. They must read the SAME entry-aware order the operator's gate did —
		// a second order here is what let the gate admit a drink while the host
		// dispatched a topical application (the container both entries claim).
		var chain = Members(Parse(HostChainFile), "TryExecute").ToList();
		Assert.True(chain.Count > 0, $"{HostChainFile}: TryExecute not found — the host half of the scan read nothing");
		Assert.Contains(
			chain.OfType<MemberAccessExpressionSyntax>(),
			access => access.ToString() == "ItemUseFamilyClassifier.Classify");
		Assert.DoesNotContain(
			chain.OfType<IdentifierNameSyntax>(),
			identifier => identifier.Identifier.ValueText is "TopicalAdmission" or "IsTopicalRemoteItem" or "InjectionAdmission");

		var tree = Members(Parse(TreeFile), "IsActuallyUsable").ToList();
		Assert.True(tree.Count > 0, $"{TreeFile}: IsActuallyUsable not found — the auto-select half of the scan read nothing");
		Assert.Contains(
			tree.OfType<MemberAccessExpressionSyntax>(),
			access => access.ToString() == "ItemUseFamilyClassifier.Classify");
	}

	[Fact]
	public void TheTopicalApply_ServesOnlyTheLimbTheRequestNamed()
	{
		var apply = Parse(ApplyFile);

		// The limb the request named, or a refusal — never a substitute: the operator's
		// pick can have gone stale by the time the patient's client runs the dose (a
		// limb dismembered in between), and landing the dose on the most injured limb is
		// exactly what the family must not do. The automatic rule stays in
		// `NativeLimbTarget.Resolve` for the chains whose -1 is a legal auto-select.
		Assert.Contains(
			apply.DescendantNodes().OfType<MemberAccessExpressionSyntax>(),
			access => access.ToString() == "NativeLimbTarget.ResolveNamed");
		Assert.DoesNotContain(
			apply.DescendantNodes().OfType<MemberAccessExpressionSyntax>(),
			access => access.ToString() == "NativeLimbTarget.Resolve");
	}
}
