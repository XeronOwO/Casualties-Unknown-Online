using System;
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

		// The limb is this body's own, resolved against its own layout.
		Assert.Contains(
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
