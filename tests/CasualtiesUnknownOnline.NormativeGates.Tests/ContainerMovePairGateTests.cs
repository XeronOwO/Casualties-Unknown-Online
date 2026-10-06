using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// The container-move PAIR gate (ticket <c>backlog/review/container-move-snapshot-only-sync.md</c>, row A1g,
/// batch `20261006-b` — the row failed in both owner directions and in a local control).
///
/// <para>
/// <c>Container.UnloadItem</c> is the game's "detach this item into the world" primitive AND the first half of a
/// container-to-container move: the expansion loop (<c>PlayerCamera.cs:1589-1590</c>) runs
/// <c>source.UnloadItem(child, null)</c> and then <c>target.LoadItem(child)</c> in one bracket. The load hook
/// classified the child by the SCENE at that instant — <c>ContainerItemPatches</c> captured
/// <c>ItemWorldSync.IsWorldItem(item)</c> in the <c>LoadItem</c> prefix — and the unload had just left the child
/// parentless, so the pair reached the peers as "the child left the world" plus "the child was picked up" while
/// the TARGET container's contents changed with no event: both viewers printed
/// <c>nested container contents changed without an event sync</c> for the target bag, and the peers materialized
/// the child as a world item and dropped it out of the owner's clone.
/// </para>
///
/// <para>
/// The rule this gate encodes: a container move is ONE operation whose carrier follows the TARGET, and the fact
/// that says where the item CAME FROM is the departure the pair opened — not the scene, which the pair has
/// already mutated. So the unload half registers the departure (with the pre-unload fact) and sends nothing
/// itself, and the load half asks that departure before it falls back to the scene capture. The report the pair
/// finally produces is the target's: the carried root's contents fact for a body-side container, the bound drop
/// for a world container, a pickup for an item that really came from the world.
/// </para>
///
/// <para>
/// Reach, stated rather than implied: the scan surface is the two hooks' own bodies, the unload patch's prefix,
/// the bridge port that carries the captured fact, the departure machine's source declaration and the carriers
/// that can re-home an item (each must resolve a pending departure, or an entry that can never settle would hold
/// the owner's snapshots back); the census floors make a renamed type or an emptied scan fail loudly instead of
/// checking nothing. The body matchers are pinned with positive and negative samples. What is NOT reached: whether
/// the runtime classification is right — the three-client acceptance run reads the divergence monitor as a
/// zero-warning row — the classification's truth table itself, which <c>ContainerLoadClassifierTests</c> pins as a
/// pure rule, and a native re-home path that has no carrier at all (the reason the hold asks whether a report is
/// still OWED rather than whether an entry merely exists).
/// </para>
/// </summary>
public class ContainerMovePairGateTests
{
	private const string ContainerItemSyncFile = "src/CasualtiesUnknownOnline.GameAdapter/Items/ContainerItemSync.cs";

	private const string ContainerItemPatchesFile = "src/CasualtiesUnknownOnline.GameAdapter/Patches/ContainerItemPatches.cs";

	private const string BridgeFile = "src/CasualtiesUnknownOnline.GameAdapter/GameAdapterBridge.cs";

	private const string BridgePortFile = "src/CasualtiesUnknownOnline.GameAdapter/IPatchBridge.cs";

	private const string PendingStateFile = "src/CasualtiesUnknownOnline.Runtime/Session/Items/DropPendingState.cs";

	private const string PickupSyncFile = "src/CasualtiesUnknownOnline.GameAdapter/Items/PickupSync.cs";

	private const string ItemSlotSyncFile = "src/CasualtiesUnknownOnline.GameAdapter/Items/ItemSlotSync.cs";

	private const string ItemWorldSyncFile = "src/CasualtiesUnknownOnline.GameAdapter/Items/ItemWorldSync.cs";

	/// <summary>The departure query the unload half registers through and the load half consumes.</summary>
	private const string DepartureRegister = "_dropState.EnterDrop(";

	/// <summary>The consume the load half asks: it also answers where the item came from.</summary>
	private const string DepartureConsume = "_dropState.TryConsumeByContainerLoad(";

	/// <summary>The pure rule that maps the departure (or the scene capture) plus the landing onto the carrier.</summary>
	private const string Classifier = "ContainerLoadClassifier.Classify(";

	/// <summary>The drop report the unload half must NOT send any more — the pair's report comes from the load.</summary>
	private const string DropSend = "SendItemDropped";

	/// <summary>The report commit the unload half must not reach for either.</summary>
	private const string ReportCommit = "CommitReport(";

	/// <summary>The pre-unload fact the patch captures: the item was part of the world before the detach.</summary>
	private const string WorldItemFact = "ItemWorldSync.IsWorldItem(item)";

	/// <summary>The port member that carries the captured fact to the container chain's owner.</summary>
	private const string UnloadBridgeMember = "OnItemUnloadedFromContainer";

	/// <summary>The bridge census floor: the declaration and its forwarding are the whole wiring, so an emptied scan fails.</summary>
	private const int MinimumUnloadBridgeSites = 2;

	/// <summary>The departure-resolution census floor: one call per resolution site across the re-home carriers.</summary>
	private const int MinimumResolutionSites = 6;

	[Fact]
	public void EveryCarrierThatReHomesAnItem_ResolvesThePendingDeparture()
	{
		var failures = new List<string>();
		var sites = 0;
		foreach (var (file, type, minimum) in ReHomeCarriers)
		{
			var found = ResolutionCalls(RepositoryPaths.ReadText(file));
			if (found < minimum)
			{
				failures.Add(
					$"{file} ({type}): {found} departure-resolution call(s), the pin wants at least {minimum} — a carrier that re-homes an item owns that item's move, so a departure left registered for it can never settle: the item is back inside a body, `TrySettle` refuses it every frame, and the owner's immediate inventory snapshots stay held back for the rest of the session (the wear path, found by this cycle's independent review)");
			}

			sites += found;
		}

		Assert.True(failures.Count == 0, "container-move pair departure-resolution gate failed" + Environment.NewLine + string.Join(Environment.NewLine, failures));
		Assert.True(
			sites >= MinimumResolutionSites,
			$"found {sites} departure-resolution call(s) across the re-home carriers — the census floor is {MinimumResolutionSites}, so the scan surface (or the machine's resolution method names) changed shape");
	}

	[Theory]
	[InlineData("if (_dropState.TryCancel(item, out var op)) { }", 1)]
	[InlineData("if (_dropState.TryConsumeByThrow(item, out var dropped)) { }", 1)]
	[InlineData("if (_dropState.TryConsumeByContainerLoad(item, out var source, out var op)) { }", 1)]
	[InlineData("if (_drops.TryCancel(item, out var op)) { }", 1)]
	[InlineData("// _dropState.TryCancel(item, out var op);", 0)]
	[InlineData("\"_dropState.TryCancel(item, out var op)\"", 0)]
	[InlineData("if (_dropState.TrySettle(itemId, frame, alive, standalone, out var settled)) { }", 0)]
	public void TheResolutionMatcher_ReadsCallsAndIgnoresMentions(string body, int expected) =>
		Assert.Equal(expected, ResolutionCalls($"internal sealed class Sample {{ private void M() {{ {body} }} }}"));

	/// <summary>
	/// The carriers that can RE-HOME an item back into an inventory or a body, and how many
	/// departure-resolution calls each declares. Named rather than globbed: these are the report
	/// seams a re-home passes through, and each one owns the item's move.
	/// </summary>
	private static readonly (string File, string Type, int MinimumSites)[] ReHomeCarriers =
	[
		(ContainerItemSyncFile, "ContainerItemSync", 2), // a container load CONSUMES the departure (it IS the move); a replacing unload cancels it
		(PickupSyncFile, "PickupSync", 1), // a landed pickup is a world pickup or a body-internal reorder — either way this report owns the move
		(ItemSlotSyncFile, "ItemSlotSync", 1), // a wear / slot move / drag re-home reports the move itself
		(ItemWorldSyncFile, "ItemWorldSync", 2), // a destruction cancels the departure; a throw consumes it
	];

	/// <summary>The machine's resolution surface: the calls that take one item's pending departure out of the set.</summary>
	private static readonly string[] ResolutionMethods = ["TryCancel", "TryConsumeByThrow", "TryConsumeByContainerLoad"];

	/// <summary>Calls to the machine's resolution surface in a source — matched by METHOD NAME, so a renamed receiver keeps the pin; a comment or a string literal is not a call.</summary>
	private static int ResolutionCalls(string source) =>
		Parse(source).DescendantNodes()
			.OfType<InvocationExpressionSyntax>()
			.Count(invocation => invocation.Expression is MemberAccessExpressionSyntax access
				&& ResolutionMethods.Contains(access.Name.Identifier.ValueText, StringComparer.Ordinal));

	[Fact]
	public void TheUnloadHalf_RegistersTheDepartureWithItsSourceAndSendsNoDropReport()
	{
		var body = RequireMethodBody(ContainerItemSyncFile, "ContainerItemSync", "OnUnloadedFromContainer");

		Assert.True(
			RegistersTheDeparture(body),
			$"{ContainerItemSyncFile}: `OnUnloadedFromContainer` must register the departure with where the item came from (`{DepartureRegister}…`) — the item is momentarily in the world, and whether it stays there is decided by the rest of the bracket (a container load re-homes it)");
		Assert.False(
			SendsItsOwnDropReport(body),
			$"{ContainerItemSyncFile}: `OnUnloadedFromContainer` still reports the drop itself (`{DropSend}` / `{ReportCommit}`) — that is the half that materialized the expansion's child as a WORLD item on both peers and dropped it out of the owner's clone (batch `20261006-b`, row A1g)");
	}

	[Fact]
	public void TheLoadHalf_AsksTheDepartureBeforeTheSceneCapture()
	{
		var body = RequireMethodBody(ContainerItemSyncFile, "ContainerItemSync", "OnLoadedIntoContainer");

		Assert.True(
			AsksTheDepartureFirst(body),
			$"{ContainerItemSyncFile}: `OnLoadedIntoContainer` must consume the departure this bracket registered (`{DepartureConsume}…`) and classify through `{Classifier}…` — without it the scene capture classifies a child the move itself just detached as having come from the world");
		Assert.True(
			body.Contains("wasWorldItem", StringComparison.Ordinal),
			$"{ContainerItemSyncFile}: the scene capture stays the FALLBACK for a load no departure opened — it is the only fact then");
	}

	[Fact]
	public void TheUnloadPatch_CapturesThePreUnloadFactInItsPrefix()
	{
		var prefix = RequireMethodBody(ContainerItemPatchesFile, "ContainerUnloadItemPatch", "Prefix");

		Assert.True(
			prefix.Contains(WorldItemFact, StringComparison.Ordinal),
			$"{ContainerItemPatchesFile}: the unload prefix must capture `{WorldItemFact}` BEFORE the detach — the postfix runs after `SetParent(null)` and the scene can no longer answer it (AGENTS.md #6: explicit state between hooks, never scene inference)");
	}

	[Fact]
	public void TheBridge_CarriesTheCapturedFactToTheContainerChain()
	{
		var sites = 0;
		var failures = new List<string>();

		var port = DeclarationText(BridgePortFile, UnloadBridgeMember);
		if (port is null)
		{
			failures.Add($"{BridgePortFile}: no `{UnloadBridgeMember}` declaration — the patch layer cannot hand the captured fact over");
		}
		else
		{
			sites++;
			if (Parameters(port).Count != 2)
			{
				failures.Add($"{BridgePortFile}: `{UnloadBridgeMember}` takes {Parameters(port).Count} parameter(s) — it must take the item AND the captured pre-unload fact, exactly as the load hook does");
			}
		}

		var forwarding = DeclarationText(BridgeFile, UnloadBridgeMember);
		if (forwarding is null)
		{
			failures.Add($"{BridgeFile}: no `{UnloadBridgeMember}` forwarding — the port member must reach the container chain's owner");
		}
		else
		{
			sites++;
			if (!forwarding.Contains("ContainerSync.OnUnloadedFromContainer", StringComparison.Ordinal))
			{
				failures.Add($"{BridgeFile}: `{UnloadBridgeMember}` no longer forwards to the container chain's owner: {forwarding}");
			}
		}

		Assert.True(failures.Count == 0, "container-move pair bridge gate failed" + Environment.NewLine + string.Join(Environment.NewLine, failures));
		Assert.True(
			sites >= MinimumUnloadBridgeSites,
			$"found {sites} `{UnloadBridgeMember}` site(s) — the census floor is {MinimumUnloadBridgeSites}, so the scan surface (or the member's name) changed shape");
	}

	[Fact]
	public void TheDepartureMachine_DeclaresWhereTheItemCameFrom()
	{
		var source = RepositoryPaths.ReadText(PendingStateFile);
		var members = EnumMembers(source, "Source");

		Assert.True(
			members.Count >= 2,
			$"{PendingStateFile} must declare an `enum Source` with the two homes a departure can come from (the carried inventory and the world) — the load half's classification reads it. Found [{string.Join(", ", members)}]");
		Assert.True(
			members.Contains("CarriedInventory") && members.Contains("World"),
			$"`Source` must name `CarriedInventory` and `World` (found [{string.Join(", ", members)}]) — the classifier's truth table is written against both");
	}

	[Theory]
	[InlineData("_dropState.EnterDrop(itemId, item, pos, op, DropPendingState.Source.World);", true)]
	[InlineData("_dropState.EnterDrop(itemId, item, pos, op);", false)]
	[InlineData("_dropState.TryCancel(item, out var op);", false)]
	public void TheDepartureRegisterMatcher_ReadsTheCallAndItsSource(string body, bool expected) =>
		Assert.Equal(expected, RegistersTheDeparture(body));

	[Theory]
	[InlineData("_reports.CommitReport(itemId, op, \"OnItemUnloadedFromContainer\", status, () => { _items.SendItemDropped(itemId, capture, pos, vel, 0, rot, default, spin); return 1; }, \"Unload\");", true)]
	[InlineData("_items.SendItemDropped(itemId, capture, pos, vel, 0, rot, default, spin);", true)]
	[InlineData("_dropState.EnterDrop(itemId, item, pos, op, DropPendingState.Source.World);", false)]
	[InlineData("_trace.End(op, itemId, \"OnItemUnloadedFromContainer\", \"Rejected\", \"Unload\");", false)]
	public void TheOwnDropReportMatcher_ReadsBothSendShapes(string body, bool expected) =>
		Assert.Equal(expected, SendsItsOwnDropReport(body));

	[Theory]
	[InlineData("if (_dropState.TryConsumeByContainerLoad(item, out var departure)) { }\nvar kind = ContainerLoadClassifier.Classify(landsInTheWorld, departure?.Source, wasWorldItem);", true)]
	[InlineData("var kind = ContainerLoadClassifier.Classify(landsInTheWorld, null, wasWorldItem);", false)]
	[InlineData("if (_dropState.TryConsumeByContainerLoad(item, out var departure)) { }", false)]
	public void ThePairConsumeMatcher_ReadsBothHalves(string body, bool expected) =>
		Assert.Equal(expected, AsksTheDepartureFirst(body));

	/// <summary>The unload half's rule: the departure is registered WITH the home it came from, so the load half can classify the move.</summary>
	private static bool RegistersTheDeparture(string body) =>
		body.Contains(DepartureRegister, StringComparison.Ordinal) && body.Contains("Source.", StringComparison.Ordinal);

	/// <summary>True when a body still reports a drop of its own — the unload half's old job, now the load half's.</summary>
	private static bool SendsItsOwnDropReport(string body) =>
		body.Contains(DropSend, StringComparison.Ordinal) || body.Contains(ReportCommit, StringComparison.Ordinal);

	/// <summary>The load half's rule: the departure is consumed AND the carrier rule is asked.</summary>
	private static bool AsksTheDepartureFirst(string body) =>
		body.Contains(DepartureConsume, StringComparison.Ordinal) && body.Contains(Classifier, StringComparison.Ordinal);

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

	/// <summary>The declaration text of a member with a given name anywhere in a file (a port member or a forwarding method).</summary>
	private static string? DeclarationText(string file, string member) =>
		Parse(RepositoryPaths.ReadText(file)).DescendantNodes()
			.OfType<MemberDeclarationSyntax>()
			.Where(declaration => declaration switch
			{
				MethodDeclarationSyntax method => string.Equals(method.Identifier.ValueText, member, StringComparison.Ordinal),
				_ => false,
			})
			.Select(declaration => declaration.ToString())
			.FirstOrDefault();

	/// <summary>The parameter list of a declaration's text, read back from its own syntax.</summary>
	private static List<string> Parameters(string declaration) =>
		[.. Parse($"internal sealed class Sample {{ {declaration} }}").DescendantNodes()
			.OfType<MethodDeclarationSyntax>()
			.SelectMany(method => method.ParameterList.Parameters)
			.Select(parameter => parameter.ToString())];

	/// <summary>The member names of an enum declared in a source file (a doc mention is not a member).</summary>
	private static List<string> EnumMembers(string source, string enumName)
	{
		var declaration = Parse(source).DescendantNodes()
			.OfType<EnumDeclarationSyntax>()
			.FirstOrDefault(candidate => string.Equals(candidate.Identifier.ValueText, enumName, StringComparison.Ordinal));

		return declaration is null ? [] : [.. declaration.Members.Select(member => member.Identifier.ValueText)];
	}

	private static SyntaxNode Parse(string source) =>
		CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview)).GetRoot();
}
