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

	private const string UseServiceFile = "src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/PlayerItemUseExecution.cs";

	private const string EligibilityFile = "src/CasualtiesUnknownOnline.GameAdapter/LocalUseItemEligibility.cs";

	/// <summary>The ONE entry-aware family order the world-drag gate and the host's dispatch both read.</summary>
	private const string ClassifierFile = "src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/ItemUseFamilyClassifier.cs";

	private const string ResultApplyFile = "src/CasualtiesUnknownOnline.GameAdapter/PlayerInteractionApply.cs";

	private const string TransformFile = "src/CasualtiesUnknownOnline.GameAdapter/Items/LimbItemTransform.cs";

	private const string ItemPatchesFile = "src/CasualtiesUnknownOnline.GameAdapter/Patches/ItemPatches.cs";

	private const string BodyItemPatchesFile = "src/CasualtiesUnknownOnline.GameAdapter/Patches/BodyItemPatches.cs";

	private const string ContainerItemPatchesFile = "src/CasualtiesUnknownOnline.GameAdapter/Patches/ContainerItemPatches.cs";

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

	/// <summary>
	/// The applied family keeps ONE id across both sides of the transform, and both sides are
	/// source shapes no behavioural test can reach (they run Unity's own deferred destroy and the
	/// game's own <c>TakeOff</c>), so they are pinned here:
	/// <list type="number">
	/// <item>the OWNER's destroy of its carried object is not the item's end. The result says the
	/// item became the limb component, and the handler attaches the same marker the local apply
	/// seam does before destroying its object — the marker <c>ItemPatches</c> reads to stay
	/// silent. Without the pair the row committed as APPLIED was overwritten with Terminal 21 ms
	/// later and the limb kept a component no row named (batch <c>20261018-u</c>'s row I1).</item>
	/// <item>a limb component's own <c>TakeOff</c> hands its revived item to the game's own
	/// <c>AutoPickUpItem</c>, and that pickup is the removal the limb seam reports — not a second
	/// gesture. The take-off scope is what keeps the pickup and container-load hooks silent, and
	/// only a take-off that resolved an applied row opens it. Without it the pickup was refused as
	/// "applied item cannot be picked up" and the refusal rolled the live object out into the
	/// world, where the reconcile killed it (row I4).</item>
	/// </list>
	/// <para>
	/// The checks are structural, not token presence, because token presence passed the mutations
	/// that matter most: making the marker unconditional (silencing every consumed item's end),
	/// opening the scope above the applied-row refusal (suppressing a genuinely new item's pickup),
	/// deleting a release (leaking the scope into later gestures) and moving a hook guard below its
	/// report (reporting the removal twice). Each check below names the ordering or the condition
	/// it needs, so those edits fail.
	/// </para>
	/// </summary>
	[Fact]
	public void TheAppliedFamily_ReportsOneRelocationPerTransformOnBothSides()
	{
		// 1. The owner's side: the marker is attached INSIDE the result's own flag, and before the
		//    deferred destroy that the marker is there to silence.
		var destroyHandler = Members(Parse(ResultApplyFile), "OnPlayerItemUseReceived").ToList();
		var marker = destroyHandler
			.OfType<InvocationExpressionSyntax>()
			.FirstOrDefault(invocation => invocation.ToString() == "item.gameObject.AddComponent<LimbAppliedItemOrigin>()");
		Assert.True(
			marker is not null,
			$"{ResultApplyFile}: OnPlayerItemUseReceived no longer attaches LimbAppliedItemOrigin to the owner's object — the deferred destroy is reported as the item's end again");
		Assert.True(
			marker!.Ancestors().OfType<IfStatementSyntax>().Any(statement =>
				statement.Condition.ToString() == "msg.BecameLimbComponent"),
			$"{ResultApplyFile}: the marker is attached UNCONDITIONALLY — every consumed item's destroy would then be silenced, not only the applied family's");
		var destroy = destroyHandler
			.OfType<InvocationExpressionSyntax>()
			.FirstOrDefault(invocation => invocation.ToString() == "Object.Destroy(item.gameObject)");
		Assert.True(destroy is not null, $"{ResultApplyFile}: the handler no longer destroys the owner's object");
		Assert.True(
			marker.SpanStart < destroy!.SpanStart,
			$"{ResultApplyFile}: the marker is attached AFTER the destroy — Unity's deferred OnDestroy no longer sees it");

		// ...and the hook reads it on the object being torn down, inside the one patch that decides.
		var destroyPatch = Parse(ItemPatchesFile).DescendantNodes().OfType<ClassDeclarationSyntax>()
			.SingleOrDefault(type => type.Identifier.ValueText == "ItemOnDestroyPatch");
		Assert.True(destroyPatch is not null, $"{ItemPatchesFile}: ItemOnDestroyPatch not found — the scan read nothing");
		var markerRead = destroyPatch!.DescendantNodes().OfType<GenericNameSyntax>()
			.FirstOrDefault(name => name.Identifier.ValueText == "GetComponent"
				&& name.TypeArgumentList.Arguments.ToString().Contains("LimbAppliedItemOrigin", StringComparison.Ordinal));
		Assert.True(
			markerRead is not null,
			$"{ItemPatchesFile}: ItemOnDestroyPatch no longer reads LimbAppliedItemOrigin — the marker silences nothing");
		Assert.True(
			markerRead!.Ancestors().OfType<IfStatementSyntax>().Any(statement =>
				statement.Statement.DescendantNodes().OfType<ReturnStatementSyntax>().Any()),
			$"{ItemPatchesFile}: the marker read is not part of a condition that returns — the hook falls through and reports the end anyway");

		// 2. The removal side: the scope is opened only AFTER the applied row resolved, and it is
		//    released on both of its exit paths.
		var transform = Parse(TransformFile);
		var begin = Members(transform, "OnLimbComponentTakeOffBegin").ToList();
		var enter = begin.OfType<AssignmentExpressionSyntax>()
			.FirstOrDefault(assignment => assignment.Left.ToString() == "_takeOffScope"
				&& assignment.Right.ToString() == "CallContext.Enter(CallContext.Origin.LimbComponentTakeOff)");
		Assert.True(
			enter is not null,
			$"{TransformFile}: OnLimbComponentTakeOffBegin no longer opens the take-off scope (an assignment of CallContext.Enter(Origin.LimbComponentTakeOff) to _takeOffScope)");
		var unresolvedReturn = begin.OfType<IfStatementSyntax>()
			.FirstOrDefault(statement => statement.Condition.ToString().Contains("itemId == 0", StringComparison.Ordinal));
		Assert.True(
			unresolvedReturn is not null,
			$"{TransformFile}: the applied-row refusal (itemId == 0) is gone — the scan can no longer tell when the scope is legal");
		Assert.True(
			enter!.SpanStart > unresolvedReturn!.Span.End,
			$"{TransformFile}: the scope is opened BEFORE the applied row resolves — a take-off with no row would suppress the pickup of a genuinely new item");

		foreach (var methodName in new[] { "OnLimbComponentTakeOffEnd", "ResetSessionState" })
		{
			Assert.True(
				Members(transform, methodName).Any(node => node.ToString() == "_takeOffScope?.Dispose()"),
				$"{TransformFile}: {methodName} no longer releases the take-off scope — it leaks into the next gesture and suppresses a real pickup");
		}

		// ...and both report hooks check it BEFORE they report, each in the patch that owns it.
		foreach (var (hookFile, className, methodName, reportCall) in new[]
		{
			(BodyItemPatchesFile, "PickUpItemPatch", "Postfix", "PatchBridge.Impl?.OnItemPickedUp(item)"),
			(ContainerItemPatchesFile, "ContainerLoadItemPatch", "Postfix", "PatchBridge.Impl?.OnItemLoadedIntoContainer(item, __state)"),
		})
		{
			var body = MembersInClass(Parse(hookFile), className, methodName).ToList();
			var guard = body.OfType<IfStatementSyntax>().FirstOrDefault(statement =>
				statement.Condition.ToString() == "CallContext.IsWithin(CallContext.Origin.LimbComponentTakeOff)");
			Assert.True(
				guard is not null,
				$"{hookFile}: {className}.{methodName} no longer refuses to report inside a limb take-off");
			Assert.True(
				guard!.Statement.DescendantNodes().OfType<ReturnStatementSyntax>().Any(),
				$"{hookFile}: {className}.{methodName} checks the take-off scope without returning — the report below still runs");
			var report = body.FirstOrDefault(node => node.ToString() == reportCall);
			Assert.True(report is not null, $"{hookFile}: {className}.{methodName} no longer reports its fact — the scan read nothing");
			Assert.True(
				guard.SpanStart < report!.SpanStart,
				$"{hookFile}: {className}.{methodName} reports BEFORE the take-off guard — the removal's own pickup reaches the host again");
		}
	}

	/// <summary>Everything declared inside one named METHOD of one named class, so a guard can be asked of the patch that owns it rather than of the whole file (the two hook files hold several patches with same-named members).</summary>
	private static IEnumerable<SyntaxNode> MembersInClass(SyntaxNode root, string className, string methodName) =>
		root.DescendantNodes()
			.OfType<ClassDeclarationSyntax>()
			.Where(type => type.Identifier.ValueText == className)
			.SelectMany(type => type.DescendantNodes())
			.OfType<MethodDeclarationSyntax>()
			.Where(method => method.Identifier.ValueText == methodName)
			.SelectMany(method => method.DescendantNodes());

	[Fact]
	public void TheWorldDragGate_AsksNoLimbRuleAndTheMedicalGateIsWhereItIsAsked()
	{
		var classifier = Parse(ClassifierFile);

		// The world drag's question is the inventory entry's family set, and a limb
		// tool has no useAction at all — so asking the limb rule HERE is exactly the
		// cross-player reach the user rejected, and on an item both entries could claim
		// it would decide by the order of a family chain instead of by the gesture.
		// The entry split moved the order into ONE classifier; the scan follows it
		// there so the rule cannot creep back in through the adapter's side.
		var inventoryFamilies = Members(classifier, "ClassifyInventory").ToList();
		Assert.True(inventoryFamilies.Count > 0, $"{ClassifierFile}: ClassifyInventory not found — the isolation scan read nothing");
		Assert.DoesNotContain(
			inventoryFamilies.OfType<IdentifierNameSyntax>(),
			identifier => identifier.Identifier.ValueText == "LimbTool");

		// Identifiers are what the walk reads, so a spelling that hides the name would
		// walk past it: the set is resolved from the file's own directives instead of
		// being assumed (decision 245 closed the same loophole in the content-kind
		// gate, where an alias-driven probe stayed green).
		var ruleNames = RuleSpellings(classifier);
		Assert.DoesNotContain(
			inventoryFamilies.OfType<IdentifierNameSyntax>(),
			identifier => ruleNames.Contains(identifier.Identifier.ValueText));

		// The adapter's side must not grow its own copy either: the entry split moved
		// the ORDER into the classifier, and the mutation this gate was written for —
		// `IsUseItem` gaining `LimbToolAdmission.IsLimbTool`, plain or behind an alias —
		// must still redden at the site the world drag actually asks.
		var adapterGate = Members(Parse(EligibilityFile), "IsUseItem")
			.Concat(Members(Parse(EligibilityFile), "FamilyOf"))
			.ToList();
		Assert.True(adapterGate.Count > 0, $"{EligibilityFile}: IsUseItem/FamilyOf not found — the adapter half of the isolation scan read nothing");
		var adapterRuleNames = RuleSpellings(Parse(EligibilityFile));
		Assert.DoesNotContain(
			adapterGate.OfType<IdentifierNameSyntax>(),
			identifier => adapterRuleNames.Contains(identifier.Identifier.ValueText));

		// ...and the medical view is where it IS asked, so the two entries stay
		// isolated by the gesture rather than by a chain's order.
		var medicalGate = Members(Parse(EligibilityFile), "IsMedicalLimbUseItem").ToList();
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
