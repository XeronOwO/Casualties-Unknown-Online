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
/// </summary>
public class TopicalChainGateTests
{
	private const string EligibilityFile = "src/CasualtiesUnknownOnline.GameAdapter/LocalUseItemEligibility.cs";

	private const string ApplyFile = "src/CasualtiesUnknownOnline.GameAdapter/NativeTopicalApply.cs";

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
		var eligibility = Parse(EligibilityFile);
		var worldDragGate = Members(eligibility, "IsUseItem")
			.Concat(Members(eligibility, "IsWorldDragFamily"))
			.ToList();
		Assert.True(worldDragGate.Count > 0, $"{EligibilityFile}: IsUseItem/IsWorldDragFamily not found — the family census read nothing");

		// The families the inventory use carries, by name: a drink and a meal ARE the
		// item's own use action and the wear placement is what the radial asks for; the
		// injection family is admitted only so the host can refuse it BY NAME, because
		// its native use action draws blood into the operator's bag.
		var names = worldDragGate
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
