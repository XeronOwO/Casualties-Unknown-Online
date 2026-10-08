using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// The cross-player LIMB-TOOL chain's content gate (ticket
/// <c>mod-cross-player-native-semantics</c>, Part B's last chain).
///
/// <para>
/// The chain's whole point is that "is this a limb tool, and what does it do" is the
/// game's own item data — <c>ItemInfo.usableOnLimb</c> with an assigned
/// <c>useLimbAction</c>, read through the content seam — and that the action itself
/// runs on the TREATED player's client against its own limb. The table that was
/// deleted (<c>RemoteLimbToolCatalog</c>) carried ten ids with transcribed
/// condition costs, body deltas, component fields and a timed ramp, so every vanilla
/// limb action it missed (and every number a game update moved) was silently wrong.
/// This gate keeps a successor table from growing back and pins the shape of the one
/// that replaced it.
/// </para>
///
/// <para>
/// Two things are checked, and the difference matters. The FIRST is a source shape (a
/// keyed initializer whose keys are string literals IS an id table) with the matcher
/// pinned by samples below. The SECOND is the capability contract, read from SYNTAX
/// rather than file text so a doc comment cannot satisfy it: the adapter's answer must
/// read <c>Item.GlobalItems</c> and the item's own <c>useLimbAction</c>, and the
/// affected side must run it on its own object of the offered item. What no source
/// gate can read is whether a live session's run really lands on the right limb: that
/// is the acceptance batch's row, and the self-check names it as a limit.
/// </para>
/// </summary>
public class LimbToolChainGateTests
{
	/// <summary>
	/// The chain's OWN Runtime sources — the seam, its default, the one admission rule
	/// and the two halves shared with the solid-food family, which reports through the
	/// same one-admission-one-report path. These are what must hold no id-keyed table,
	/// whatever they are named later; the deleted <c>RemoteLimbToolCatalog.cs</c> is
	/// where the old one lived, and the wear chain's own gate enumerates the remaining
	/// catalogs of the directory.
	/// </summary>
	private static readonly string[] LimbToolChainSources =
	[
		"src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/ILimbUseSemantics.cs",
		"src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/NoLimbUseSemantics.cs",
		"src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/LimbToolAdmission.cs",
		"src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/ItemActionGrants.cs",
		"src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/PlayerItemActionOutcomeService.cs",
	];

	private const string FactsFile = "src/CasualtiesUnknownOnline.GameAdapter/Content/GameLimbUseFacts.cs";

	private const string SeamFile = "src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/ILimbUseSemantics.cs";

	private const string RuleFile = "src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/LimbToolAdmission.cs";

	private const string ApplyFile = "src/CasualtiesUnknownOnline.GameAdapter/NativeLimbToolApply.cs";

	private const string DragUseFile = "src/CasualtiesUnknownOnline.GameAdapter/CrossPlayerDragUse.cs";

	private const string UseServiceFile = "src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/PlayerItemUseService.cs";

	private const string EligibilityFile = "src/CasualtiesUnknownOnline.GameAdapter/LocalUseItemEligibility.cs";

	private const string ResultApplyFile = "src/CasualtiesUnknownOnline.GameAdapter/PlayerInteractionApply.cs";

	/// <summary>
	/// An id table's shape: a collection initializer whose keys are string literals.
	/// Deliberately narrow, and the same shape the two sibling chain gates read — a
	/// dictionary built from content at runtime is not a table, and a single string
	/// constant is not one either. Samples below pin both directions.
	/// </summary>
	private static readonly Regex IdTableInitializer = new(
		@"\[\s*""[^""]+""\s*\]\s*=",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);

	private static SyntaxNode Parse(string relativePath) =>
		CSharpSyntaxTree
			.ParseText(RepositoryPaths.ReadText(relativePath), new CSharpParseOptions(LanguageVersion.Preview))
			.GetRoot();

	[Fact]
	public void TheRuntimeLimbToolChain_HoldsNoIdKeyedItemTable()
	{
		var withTables = LimbToolChainSources
			.Where(file => IdTableInitializer.IsMatch(RepositoryPaths.ReadText(file)))
			.ToList();

		Assert.True(
			withTables.Count == 0,
			$"the limb-tool chain's Runtime source(s) [{string.Join(", ", withTables)}] declare an id-keyed table initializer — the family's answer must stay the item's own limb action (`ILimbUseSemantics.IsLimbActionItem` over `ItemInfo.usableOnLimb` and `useLimbAction`), never a transcribed id table; the deleted `RemoteLimbToolCatalog` was that table, and its ten rows are what kept every unlisted vanilla limb action off another player and every moved number silently stale");

		// The scan must have READ something: a renamed source would make the loop
		// above check nothing at all and pass.
		foreach (var file in LimbToolChainSources)
		{
			Assert.True(
				RepositoryPaths.ReadText(file).Length > 0,
				$"{file} is named as a limb-tool chain source but is empty — the scan surface is stale");
		}
	}

	[Fact]
	public void TheFamilysAnswer_IsTheGamesOwnItemRegistryAndItsOwnLimbAction()
	{
		var facts = Parse(FactsFile);

		// The registry is what mod content registers into: an answer from anywhere
		// else would re-create the ceiling this family exists to remove.
		Assert.Contains(
			facts.DescendantNodes().OfType<MemberAccessExpressionSyntax>(),
			access => access.ToString() == "Item.GlobalItems");

		// The two data facts, by name: the flag the native dispatch branches on and
		// the delegate it calls.
		var identifiers = facts.DescendantNodes().OfType<IdentifierNameSyntax>()
			.Select(identifier => identifier.Identifier.ValueText)
			.ToHashSet(StringComparer.Ordinal);
		Assert.Contains("usableOnLimb", identifiers);
		Assert.Contains("useLimbAction", identifiers);

		// ...and the container exclusion is part of the same answer, because a liquid
		// carrier belongs to the two liquid chains.
		Assert.Contains("LiquidItemInfo", identifiers);

		// The seam declares the question the whole family asks.
		var seam = Parse(SeamFile);
		Assert.Contains(
			seam.DescendantNodes().OfType<MethodDeclarationSyntax>(),
			method => method.Identifier.ValueText == "IsLimbActionItem");
	}

	[Fact]
	public void TheRun_LandsOnTheAffectedSidesOwnLimbThroughItsOwnObjectOfTheItem()
	{
		var apply = Parse(ApplyFile);

		// The game's own delegate, run — not a CUO effect model.
		Assert.Contains(
			apply.DescendantNodes().OfType<MemberAccessExpressionSyntax>(),
			access => access.Name.Identifier.ValueText == "useLimbAction");

		// On the treated player's OWN object of the offered item, which is what makes
		// the item's condition cost and the limb component the game's own numbers.
		Assert.Contains(
			apply.DescendantNodes().OfType<MemberAccessExpressionSyntax>(),
			access => access.ToString() == "StandingItems.Is");
		Assert.Contains(
			apply.DescendantNodes().OfType<MemberAccessExpressionSyntax>(),
			access => access.ToString() == "domains.StandingMaterializer.TryGetStandingItem");

		// The limb is this body's own, resolved against its own layout — and it is the
		// limb the request NAMED, with no substitute: this family's whole meaning is
		// which limb the operator picked (decision 246), the host already refuses a
		// request that names none, and the automatic "most injured limb" rule belongs
		// to the INJECTION chain, whose -1 IS a legal auto-select. A fallback here
		// lands the tool on a limb nobody picked.
		Assert.Contains(
			apply.DescendantNodes().OfType<MemberAccessExpressionSyntax>(),
			access => access.ToString() == "NativeLimbTarget.ResolveNamed");
		Assert.DoesNotContain(
			apply.DescendantNodes().OfType<MemberAccessExpressionSyntax>(),
			access => access.ToString() == "NativeLimbTarget.Resolve");

		// Inside the medical capture scope, so the clip the delegate plays at the
		// treated limb is relayed exactly as a local application's is.
		Assert.Contains(
			apply.DescendantNodes().OfType<MemberAccessExpressionSyntax>(),
			access => access.ToString() == "CallContext.Origin.CharacterMedicalUse");

		// The item's post-run state goes back to its owner through the shared
		// affected-side report, with this family's own observation of a consumed object.
		Assert.Contains(
			apply.DescendantNodes().OfType<InvocationExpressionSyntax>(),
			invocation => invocation.Expression is MemberAccessExpressionSyntax access && access.Name.Identifier.ValueText == "SendItemActionOutcome");

		// The host asks for the run by name on the request half and only there.
		var useService = Parse(UseServiceFile);
		Assert.Contains(
			useService.DescendantNodes().OfType<AssignmentExpressionSyntax>(),
			assignment => assignment.Left.ToString() == "TargetRunsLimbAction" && assignment.Right.ToString() == "true");

		// ...and the family is reachable from the MEDICAL VIEW only: the inventory-use
		// gesture (the world drag) carries what the game can use from the inventory, and
		// a limb tool has no useAction at all. A request that names no limb is refused by
		// the host, so neither entry can be reached through the other.
		Assert.DoesNotContain(
			Parse(DragUseFile).DescendantNodes().OfType<IdentifierNameSyntax>(),
			identifier => identifier.Identifier.ValueText == "LimbToolAdmission");
		Assert.Contains(
			useService.DescendantNodes().OfType<BinaryExpressionSyntax>(),
			binary => binary.ToString().Contains("limbIndex < 0", StringComparison.Ordinal));

		var resultApply = Parse(ResultApplyFile);
		Assert.Contains(
			resultApply.DescendantNodes().OfType<MemberAccessExpressionSyntax>(),
			access => access.ToString() == "msg.TargetRunsLimbAction");
	}

	[Fact]
	public void TheWorldDragGate_AsksNoLimbRuleAndTheMedicalGateIsWhereItIsAsked()
	{
		var eligibility = Parse(EligibilityFile);

		// The world drag's question is `IsUseItem` over `FamilyOf`, and a limb tool has
		// no useAction at all — so asking the limb rule HERE is exactly the cross-player
		// reach the user rejected, and on an item both entries could claim it would
		// decide by the order of a family chain instead of by the item's own data.
		// Scoped to those two members on purpose: this same file's medical gate
		// legitimately asks the rule, and a doc comment that explains the exclusion is
		// not a call (the walk reads nodes, so comments are deliberately outside it).
		var useItem = Members(eligibility, "IsUseItem").ToList();
		var familyOf = Members(eligibility, "FamilyOf").ToList();
		Assert.True(useItem.Count > 0, $"{EligibilityFile}: IsUseItem not found — the isolation scan read nothing");
		Assert.True(familyOf.Count > 0, $"{EligibilityFile}: FamilyOf not found — the isolation scan read nothing");
		var worldDragGate = useItem.Concat(familyOf).ToList();
		Assert.DoesNotContain(
			worldDragGate.OfType<IdentifierNameSyntax>(),
			identifier => identifier.Identifier.ValueText == "LimbToolAdmission");

		// Identifiers are what the walk reads, so a spelling that hides the name would
		// walk past it: the set is resolved from the file's own directives instead of
		// being assumed (decision 245 closed the same loophole in the content-kind
		// gate, where an alias-driven probe stayed green).
		var ruleNames = RuleSpellings(eligibility);
		Assert.DoesNotContain(
			worldDragGate.OfType<IdentifierNameSyntax>(),
			identifier => ruleNames.Contains(identifier.Identifier.ValueText));

		// ...and the medical view is where it IS asked, so the two entries stay
		// isolated by the item's own data rather than by a chain's order.
		var medicalGate = Members(eligibility, "IsMedicalLimbUseItem").ToList();
		Assert.True(medicalGate.Count > 0, $"{EligibilityFile}: IsMedicalLimbUseItem not found — the medical half of the isolation scan read nothing");
		Assert.Contains(
			medicalGate.OfType<MemberAccessExpressionSyntax>(),
			access => access.ToString() == "LimbToolAdmission.IsLimbTool");
	}

	/// <summary>
	/// Every identifier that DENOTES the limb rule inside the parsed file: its own name,
	/// any alias the file declares for it (convention 7 prefers a using alias over a
	/// fully qualified name, so the spelling is RESOLVED rather than forbidden), and —
	/// under a using-static, which leaves them bare — the rule's own member names.
	/// </summary>
	private static HashSet<string> RuleSpellings(SyntaxNode file)
	{
		var names = new HashSet<string>(StringComparer.Ordinal) { "LimbToolAdmission" };
		foreach (var directive in file.DescendantNodes().OfType<UsingDirectiveSyntax>())
		{
			if (directive.Name?.ToString().EndsWith("LimbToolAdmission", StringComparison.Ordinal) != true)
			{
				continue;
			}

			names.Add(directive.Alias?.Name.ToString() ?? "LimbToolAdmission");
			if (directive.StaticKeyword.IsKind(SyntaxKind.StaticKeyword))
			{
				names.UnionWith(Parse(RuleFile)
					.DescendantNodes()
					.OfType<MethodDeclarationSyntax>()
					.Select(method => method.Identifier.ValueText));
			}
		}

		return names;
	}

	/// <summary>Everything declared inside the named members, so an isolation question can be asked of one gate rather than of a whole file.</summary>
	private static IEnumerable<SyntaxNode> Members(SyntaxNode root, string methodName) =>
		root.DescendantNodes()
			.OfType<MethodDeclarationSyntax>()
			.Where(method => method.Identifier.ValueText == methodName)
			.SelectMany(method => method.DescendantNodes());

	[Fact]
	public void TheAdmissionRule_RefusesEveryItemAnUnmigratedChainClaims()
	{
		// The rule's own exclusions are the chain's boundary: an item that reaches this
		// family is one no session chain owns, and every claim listed here names the
		// chain that must migrate before its line can leave the rule.
		var rule = Parse(RuleFile);
		var claimed = rule.DescendantNodes().OfType<MemberAccessExpressionSyntax>()
			.Select(access => access.ToString())
			.ToHashSet(StringComparer.Ordinal);

		foreach (var expected in new[]
		{
			"RemoteHealProfiles.IsHealItem",
			"RemoteBandageMinigameCatalog.IsBandageItem",
			"RemoteOtherMedicalCatalog.IsAed",
			"RemoteOtherMedicalCatalog.IsManualDefibrillator",
			"RemoteOtherMedicalCatalog.IsAmputationTool",
			"RemoteOtherMedicalCatalog.IsDislocationWrench",
			"ShrapnelStartValidator.IsTweezers",
		})
		{
			Assert.Contains(expected, claimed);
		}

		// The solid-food claim is asked through the item's OWN data rather than by id
		// (bulbskin and xalorissponge carry both actions — Item.cs:2534/2571), and it is
		// the one claim whose absence would be silent rather than a refusal: the host's
		// chain asks the solid-food rule first, so without this line a wound-view gesture
		// on such an item would make the treated player eat it. There is no id list to
		// pin here, so the RULE is what must be read.
		Assert.Contains(
			rule.DescendantNodes().OfType<MemberAccessExpressionSyntax>(),
			access => access.ToString() == "SolidFoodAdmission.Classify");
	}

	/// <summary>
	/// The isolation scan is scoped to MEMBERS, so its contract is that it reads the
	/// right one: in a synthetic source where one method asks the limb rule and its
	/// neighbour does not, the two must answer differently. Without this the scoping
	/// could silently widen to the whole file — which the real gate would then always
	/// fail, because the medical half legitimately asks the rule — or narrow to
	/// nothing, which is why every real scan also carries a "found something" floor.
	/// </summary>
	[Fact]
	public void TheMemberScoping_ReadsTheNamedMemberAndNotItsNeighbour()
	{
		var root = CSharpSyntaxTree.ParseText(
			"""
			internal static class Sample
			{
				internal static bool Asks(string id) => LimbToolAdmission.IsLimbTool(id);

				internal static bool DoesNot(string id) => SolidFoodAdmission.IsFeedable(id);
			}
			""",
			new CSharpParseOptions(LanguageVersion.Preview)).GetRoot();

		Assert.Contains(
			Members(root, "Asks").OfType<MemberAccessExpressionSyntax>(),
			access => access.ToString() == "LimbToolAdmission.IsLimbTool");
		Assert.DoesNotContain(
			Members(root, "DoesNot").OfType<IdentifierNameSyntax>(),
			identifier => identifier.Identifier.ValueText == "LimbToolAdmission");
		Assert.Empty(Members(root, "NotDeclared"));
	}

	[Theory]
	[InlineData("[\"medicalsuture\"] = new(\"medicalsuture\", 0.51f),", true)]
	// The deleted table's own shape — one keyed initializer per row.
	[InlineData("[\"splint\"] = new(\"splint\", ConditionCost: 1f),", true)]
	// Not a table: a dictionary filled from content at runtime.
	[InlineData("foreach (var pair in registry) { map[pair.Key] = pair.Value; }", false)]
	// Not a table: the seam's own signature and a single constant.
	[InlineData("bool IsLimbActionItem(string itemId);", false)]
	[InlineData("private const string Tools = \"splint,tourniquet\";", false)]
	// Not a table: a constant subscript read.
	[InlineData("var first = known[0];", false)]
	public void TheIdTableMatcher_ReadsAKeyedInitializer(string snippet, bool expected) =>
		Assert.Equal(expected, IdTableInitializer.IsMatch(snippet));
}
