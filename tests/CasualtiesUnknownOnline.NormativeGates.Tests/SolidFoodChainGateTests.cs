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
/// The cross-player SOLID-FOOD chain's content gate (ticket
/// <c>mod-cross-player-solid-food-semantics</c>, step 3).
///
/// <para>
/// The chain's whole point is that "is this food, and what does eating it do" is
/// the item's OWN use action — a delegate whose compiled body calls
/// <c>Body.Eat</c>/<c>Body.Drink</c> — and never a hand-transcribed id table. The
/// table that was deleted (<c>RemoteConsumeCatalog</c>) carried 25 vanilla ids
/// with their hunger/thirst values copied out of <c>Item.SetupItems()</c>, so
/// every mod food and every vanilla edible it never listed (the 18 filed under
/// <c>category = "custom"</c>: <c>geofruit</c>, <c>browncap</c>, <c>popfruit</c>,
/// …) could be carried, dropped and eaten locally but never offered to another
/// player. This gate keeps a successor table from growing back and pins the
/// shape the migration settled on.
/// </para>
///
/// <para>
/// What it reads, and what it cannot: the id-table check is a source shape (a
/// keyed initializer whose keys are string literals IS a table) with the matcher
/// pinned by samples below, the content check walks SYNTAX rather than file text
/// so a doc comment cannot satisfy it, and the execution checks pin the three
/// places the eat's shape lives (the affected side runs it, the request half asks
/// for that, the eater's report carries it back). What no source gate can read is
/// whether a live session's eat really lands on the right body: that is the
/// acceptance batch's row, and the self-check names it as a limit.
/// </para>
/// </summary>
public class SolidFoodChainGateTests
{
	/// <summary>
	/// The chain's OWN Runtime sources — the seam, its default, the rule, the shape,
	/// the admission table and the affected side's host half (both shared with the
	/// limb-tool chain, which reports through the same one-admission-one-report path),
	/// plus where a use's item state lands. These are what must hold no id-keyed
	/// table, whatever they are named later; the deleted <c>RemoteConsumeCatalog.cs</c>
	/// is where the old one lived, and the wear chain's own gate enumerates the
	/// remaining catalogs of the directory.
	/// </summary>
	private static readonly string[] ChainSources =
	[
		"src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/ISolidFoodSemantics.cs",
		"src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/NoSolidFoodSemantics.cs",
		"src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/SolidFoodAdmission.cs",
		"src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/SolidFoodVerdict.cs",
		"src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/ItemActionGrants.cs",
		"src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/PlayerItemActionOutcomeService.cs",
		"src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/PlayerItemUseCommit.cs",
		"src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/PlayerItemUseExecution.cs",
		"src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/ItemUseFamilyClassifier.cs",
		"src/CasualtiesUnknownOnline.GameAdapter/Content/GameDrinkActionFacts.cs",
	];

	private const string ExecutionFile = "src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/PlayerItemUseExecution.cs";

	private const string ClassifierFile = "src/CasualtiesUnknownOnline.Runtime/Session/PlayerInteraction/ItemUseFamilyClassifier.cs";

	private const string FactsFile = "src/CasualtiesUnknownOnline.GameAdapter/Content/GameSolidFoodFacts.cs";

	private const string DrinkFactsFile = "src/CasualtiesUnknownOnline.GameAdapter/Content/GameDrinkActionFacts.cs";

	private const string UseActionReaderFile = "src/CasualtiesUnknownOnline.GameAdapter/Content/UseActionInstructions.cs";

	private const string DrinkHandlerFile = "src/CasualtiesUnknownOnline.GameAdapter/RemoteDrinkUseHandler.cs";

	private const string EatFile = "src/CasualtiesUnknownOnline.GameAdapter/NativeSolidFoodEat.cs";

	private const string UseReportFile = "src/CasualtiesUnknownOnline.GameAdapter/Items/ItemUseSync.cs";

	private const string ApplyFile = "src/CasualtiesUnknownOnline.GameAdapter/PlayerInteractionApply.cs";

	/// <summary>
	/// An id table's shape: a collection initializer whose keys are string literals.
	/// Deliberately narrow, and the same shape the wear chain's gate reads — a
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

	/// <summary>Everything declared inside the named member, so a question can be asked of one arm rather than of a whole file.</summary>
	private static IEnumerable<SyntaxNode> Members(SyntaxNode root, string methodName) =>
		root.DescendantNodes()
			.OfType<MethodDeclarationSyntax>()
			.Where(method => method.Identifier.ValueText == methodName)
			.SelectMany(method => method.DescendantNodes());

	[Fact]
	public void TheRuntimeSolidFoodChain_HoldsNoIdKeyedItemTable()
	{
		var withTables = ChainSources
			.Where(file => IdTableInitializer.IsMatch(RepositoryPaths.ReadText(file)))
			.ToList();

		Assert.True(
			withTables.Count == 0,
			$"the solid-food chain's Runtime source(s) [{string.Join(", ", withTables)}] declare an id-keyed table initializer — the family's answer must stay the item's own use action (`ISolidFoodSemantics` over the delegate's compiled body), never a transcribed id table; the deleted `RemoteConsumeCatalog` was that table, and its 25 rows are what kept every mod food and every unlisted vanilla edible off another player");

		// The scan must have READ something: a renamed source would make the loop
		// above check nothing at all and pass.
		foreach (var file in ChainSources)
		{
			Assert.True(
				RepositoryPaths.ReadText(file).Length > 0,
				$"{file} is named as a solid-food chain source but is empty — the scan surface is stale");
		}
	}

	[Fact]
	public void TheFamilyVerdict_IsTheItemsOwnUseActionReadByTheGamesOwnInstructionReader()
	{
		var facts = Parse(FactsFile);
		var reader = Parse(UseActionReaderFile);

		// The registry is what mod content registers into: an answer from anywhere
		// else would re-create the ceiling this family exists to remove.
		Assert.Contains(
			facts.DescendantNodes().OfType<MemberAccessExpressionSyntax>(),
			access => access.ToString() == "Item.GlobalItems");

		// The use action is a delegate, so its own compiled body is the only place
		// that knows what it does — read through the reader this adapter's own
		// transpilers are handed instructions by, never by RUNNING it (a food action
		// writes the body, the item, sounds and, for two of them, a new object). The
		// reader is shared with the drink classifier, so the gate pins BOTH the call
		// site and the one place that names Harmony's reader.
		Assert.Contains(
			facts.DescendantNodes().OfType<MemberAccessExpressionSyntax>(),
			access => access.ToString() == "UseActionInstructions.Read");
		Assert.Contains(
			facts.DescendantNodes().OfType<IdentifierNameSyntax>(),
			identifier => identifier.Identifier.ValueText == "useAction");
		Assert.Contains(
			reader.DescendantNodes().OfType<MemberAccessExpressionSyntax>(),
			access => access.ToString() == "PatchProcessor.GetOriginalInstructions");

		// The three questions, by the native calls they look for: does it feed the
		// body, does it hand the eater a replacement object, does it destroy the item.
		var literals = facts.DescendantNodes().OfType<LiteralExpressionSyntax>()
			.Select(literal => literal.Token.ValueText)
			.ToHashSet(StringComparer.Ordinal);
		foreach (var expected in new[] { "Eat", "Drink", "PickUpItem", "AutoPickUpItem", "Instantiate", "Create", "Destroy" })
		{
			Assert.Contains(expected, literals);
		}
	}

	/// <summary>
	/// The DRINK family's definition check, the same shape one chain over: the answer must come from the
	/// item's own use action read through the shared instruction reader, and it must look for the game's own
	/// <c>WaterContainerItem.Drink</c> call — never a hand list of the two blood bags, and never the action
	/// itself run to find out (running it is the destructive defect the classifier exists to prevent).
	/// </summary>
	[Fact]
	public void TheDrinkVerdict_IsTheItemsOwnUseActionReadByTheGamesOwnInstructionReader()
	{
		var facts = Parse(DrinkFactsFile);

		Assert.Contains(
			facts.DescendantNodes().OfType<MemberAccessExpressionSyntax>(),
			access => access.ToString() == "Item.GlobalItems");
		Assert.Contains(
			facts.DescendantNodes().OfType<MemberAccessExpressionSyntax>(),
			access => access.ToString() == "UseActionInstructions.Read");
		Assert.Contains(
			facts.DescendantNodes().OfType<IdentifierNameSyntax>(),
			identifier => identifier.Identifier.ValueText == "useAction");

		var literals = facts.DescendantNodes().OfType<LiteralExpressionSyntax>()
			.Select(literal => literal.Token.ValueText)
			.ToHashSet(StringComparer.Ordinal);
		Assert.Contains("Drink", literals);
		Assert.Contains(
			facts.DescendantNodes().OfType<IdentifierNameSyntax>(),
			identifier => identifier.Identifier.ValueText == "WaterContainerItem");
	}

	/// <summary>
	/// The fix's actual claim: the definition is classified BEFORE anything runs. A source gate is the only
	/// executable check here (the IL and the Unity objects cannot be built in this host), so it pins the ORDER
	/// of the three sites inside <c>TryMeasure</c>: the classifier call, the capture window opening and the
	/// native action invocation. Moving the switch back after either one is the destructive defect returning
	/// with every other check green.
	/// </summary>
	[Fact]
	public void TheDrinkRefusal_HappensBeforeTheActionRuns()
	{
		var handler = Parse(DrinkHandlerFile);

		var classify = handler.DescendantNodes().OfType<InvocationExpressionSyntax>()
			.FirstOrDefault(invocation => invocation.Expression.ToString() == "GameDrinkActionFacts.Classify");
		Assert.True(classify is not null, "the handler must classify the definition before measuring a dose");

		var captureOpens = handler.DescendantNodes().OfType<AssignmentExpressionSyntax>()
			.Where(assignment => assignment.Left.ToString() == "_capture")
			.Select(assignment => assignment.SpanStart)
			.FirstOrDefault();
		Assert.True(captureOpens > classify!.SpanStart,
			"the capture window must open only after the definition is classified");

		var actionRuns = handler.DescendantNodes().OfType<InvocationExpressionSyntax>()
			.FirstOrDefault(invocation => invocation.Expression.ToString() == "info.useAction");
		Assert.True(actionRuns is not null, "the dose measurement runs the item's own use action");
		Assert.True(actionRuns!.SpanStart > classify.SpanStart,
			"the item's own use action must run only after the definition is classified");
	}

	[Fact]
	public void TheEat_RunsOnTheAffectedSideThroughTheGamesOwnUseEntry()
	{
		var eat = Parse(EatFile);

		// The game's own entry point, and the game's own category: the eat must be Body.UseItem on an
		// item the resolution produced, and the data guard must refuse an id that is a world row —
		// both arms ask it, the carried one directly and the hand case through the standing classifier.
		Assert.Contains(
			eat.DescendantNodes().OfType<InvocationExpressionSyntax>(),
			invocation => invocation.Expression is MemberAccessExpressionSyntax access && access.Name.Identifier.ValueText == "UseItem");
		Assert.Contains(
			eat.DescendantNodes().OfType<MemberAccessExpressionSyntax>(),
			access => access.ToString() == "ItemWorldSync.IsWorldItem");
		Assert.Contains(
			eat.DescendantNodes().OfType<MemberAccessExpressionSyntax>(),
			access => access.ToString() == "StandingItems.Is");

		// Inside the item-use sound scope, so the crunch and the item's other clips are
		// relayed at the eater's position exactly as a local eat's are.
		Assert.Contains(
			eat.DescendantNodes().OfType<MemberAccessExpressionSyntax>(),
			access => access.ToString() == "CallContext.Origin.CharacterItemUse");

		// The host asks for that by name on the request half, and only there: the
		// second result it publishes from the eater's report must not ask again.
		var useService = Parse(ExecutionFile);
		Assert.Contains(
			useService.DescendantNodes().OfType<AssignmentExpressionSyntax>(),
			assignment => assignment.Left.ToString() == "TargetEatsTheItem" && assignment.Right.ToString() == "true");

		var apply = Parse(ApplyFile);
		Assert.Contains(
			apply.DescendantNodes().OfType<MemberAccessExpressionSyntax>(),
			access => access.ToString() == "msg.TargetEatsTheItem");
	}

	[Fact]
	public void TheEatingSidesReport_IsTheStandingObjectsOwnRouteToItsOwner()
	{
		// The use report's third route: a use of a standing object is a fact about the
		// OWNER's item, so it goes back through the host instead of being published as
		// this side's own — the direction the family's whole two-step shape exists for.
		var report = Parse(UseReportFile);
		Assert.Contains(
			report.DescendantNodes().OfType<InvocationExpressionSyntax>(),
			invocation => invocation.Expression is MemberAccessExpressionSyntax access && access.Name.Identifier.ValueText == "SendItemActionOutcome");
		Assert.Contains(
			report.DescendantNodes().OfType<MemberAccessExpressionSyntax>(),
			access => access.ToString() == "StandingItems.Is");

		// ...and the family's gate keeps that route to the family: the same hook also
		// reports a gun's state (GunStateSync calls OnItemUsed), and a standing gun is
		// not a food. Without this arm every standing gun's state change would send the
		// host a report it must refuse.
		Assert.Contains(
			report.DescendantNodes().OfType<MemberAccessExpressionSyntax>(),
			access => access.ToString() == "SolidFoodAdmission.IsFeedable");

		// And the ONE classifier's inventory arm asks the admission rather than a
		// catalog, which is what keeps the shape that hands the eater a replacement
		// object out of the auto-select and out of the host's reach.
		var inventoryFamilies = Members(Parse(ClassifierFile), "ClassifyInventory").ToList();
		Assert.True(inventoryFamilies.Count > 0, $"{ClassifierFile}: ClassifyInventory not found — the solid-food gate read nothing");
		Assert.Contains(
			inventoryFamilies.OfType<MemberAccessExpressionSyntax>(),
			access => access.ToString() == "SolidFoodAdmission.IsFeedable");
	}

	/// <summary>
	/// The eat runs on the EATER's carried item when the gift made it theirs, and otherwise on the standing object
	/// for the GIVER's item — "eat from my hand", the user's ruling of 2026-10-25, which rejected the transfer-first
	/// shape outright because the target may have no free slot. The order matters: the carried lookup comes first so
	/// the common case finds a real slot, and the standing arm stays as the hand case rather than being deleted.
	/// (The hand case's own slot steps are the ticket's next step, not something this gate can prove.)
	/// </summary>
	[Fact]
	public void TheEat_ResolvesTheEatersOwnCarriedItemFirst_ThenTheHandCase()
	{
		var eat = Parse(EatFile);

		var carried = eat.DescendantNodes().OfType<InvocationExpressionSyntax>()
			.FirstOrDefault(invocation => invocation.Expression is MemberAccessExpressionSyntax access
				&& access.Expression.ToString() == "CarriedItemLocator");
		Assert.True(carried is not null,
			$"{EatFile}: the eat must resolve the EATER's own carried item (CarriedItemLocator) — that is the case where the game's own slot steps work");

		var handCase = eat.DescendantNodes().OfType<InvocationExpressionSyntax>()
			.FirstOrDefault(invocation => invocation.Expression.ToString() == "domains.StandingMaterializer.TryGetStandingItem");
		Assert.True(handCase is not null,
			$"{EatFile}: the HARD hand case (the item is the giver's) must still resolve the standing object — the transfer-first shape was rejected by the user");
		Assert.True(carried!.SpanStart < handCase!.SpanStart,
			$"{EatFile}: the carried lookup must come FIRST — its own slot steps are what make the common case native");

		var useItem = eat.DescendantNodes().OfType<InvocationExpressionSyntax>()
			.FirstOrDefault(invocation => invocation.Expression is MemberAccessExpressionSyntax access && access.Name.Identifier.ValueText == "UseItem");
		Assert.True(useItem is not null, $"{EatFile}: the eat still runs the game's own Body.UseItem");
		Assert.True(useItem!.SpanStart > handCase.SpanStart,
			$"{EatFile}: Body.UseItem must run on whichever item the resolution produced");
	}

	[Theory]
	[InlineData("[ \"bread\" ] = new(\"bread\", 0.34f),", true)]
	// The deleted table's own shape — one keyed initializer per row.
	[InlineData("[\"geofruit\"] = new(\"geofruit\", 0.5f),", true)]
	// Not a table: a dictionary filled from content at runtime.
	[InlineData("foreach (var pair in registry) { map[pair.Key] = pair.Value; }", false)]
	// Not a table: the seam's own signature and a single constant.
	[InlineData("SolidFoodVerdict Classify(string itemId);", false)]
	[InlineData("private const string Foods = \"bread,cake\";", false)]
	// Not a table: a constant subscript read.
	[InlineData("var first = known[0];", false)]
	public void TheIdTableMatcher_ReadsAKeyedInitializer(string snippet, bool expected) =>
		Assert.Equal(expected, IdTableInitializer.IsMatch(snippet));
}
