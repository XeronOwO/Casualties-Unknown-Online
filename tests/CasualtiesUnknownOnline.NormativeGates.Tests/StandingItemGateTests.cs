using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// The standing item object's build, lifetime and transition gate (ticket
/// <c>backlog/done/mod-cross-player-solid-food-semantics.md</c>, §6 step 2).
///
/// <para>
/// The category (step 1) made every classifier answer for an object the data carries as a member's CARRIED
/// row. Step 2 creates those objects, and the recipe is written down in the ticket precisely because its
/// decisions are invisible from the objects themselves: an object built without the switches looks exactly
/// like a world item the instant it exists, one built in a real <c>Container</c> hands the game's own
/// container machinery (load refusals with player-facing alerts, and
/// <c>ContainerBroke</c>/<c>UnloadAllItems</c> spilling standing children into the local world as live
/// items) the power to duplicate another player's bag, and the components that must not run are the ones
/// that would act on OWNER-LOCAL state this side does not have (a restored grappling-hook or gun flag makes
/// the original script throw, play or fire for real). This gate pins the SOURCE SHAPE of all three, plus the
/// lifetime wiring and the world-transition sites.
/// </para>
///
/// <para>
/// Reach: the named bodies, resolved through Roslyn, with a census floor so a renamed type or an emptied
/// scan fails loudly instead of checking nothing; the matchers are pinned by positive and negative samples,
/// and the assertions are written against SYNTAX rather than identifier presence wherever a renamed or
/// inverted statement would otherwise slip through. Three limits are stated rather than implied. The
/// disabled-component list is an EXACT-EQUALITY contract, so it fails when the code and the list disagree,
/// not when both are missing a tenth component — the criterion ("components that act on owner-local state
/// this side does not have"), the proxy path named as the precedent, the ticket's per-name evidence and the
/// batch's per-session `[ERR][Unity:Exception]` count are the substitute, and a component that acts SILENTLY
/// (no throw, no sound, no visible change) is caught by reading, not by this gate. Whether a real session
/// then materializes, aligns and retires the objects is the acceptance batch's readings (§7), because it
/// needs the game's own scene. And the identifier-presence assertions (`Reads`) catch a deleted statement,
/// not a disabled one — the syntax-level matchers are what carry the inversions.
/// </para>
/// </summary>
public class StandingItemGateTests
{
	private const string MaterializerFile = "src/CasualtiesUnknownOnline.GameAdapter/Items/StandingItemMaterializer.cs";

	private const string RecipeFile = "src/CasualtiesUnknownOnline.GameAdapter/Items/StandingItemRecipe.cs";

	private const string SceneOpsFile = "src/CasualtiesUnknownOnline.GameAdapter/Items/RemoteItemSceneOps.cs";

	private const string ApplicationFile = "src/CasualtiesUnknownOnline.GameAdapter/Items/ItemApplication.cs";

	private const string CookReplayFile = "src/CasualtiesUnknownOnline.GameAdapter/Items/ItemCookReplayApplier.cs";

	private const string ReconcileFile = "src/CasualtiesUnknownOnline.GameAdapter/Items/ItemReconcile.cs";

	private const string BindingFile = "src/CasualtiesUnknownOnline.GameAdapter/GameAdapterSessionBinding.cs";

	private const string AdapterFile = "src/CasualtiesUnknownOnline.GameAdapter/GameAdapter.cs";

	/// <summary>
	/// The components a standing object disables: §6.3's four writers of things outside the object, plus
	/// every component that would act on restored OWNER-LOCAL state this side does not have — the display
	/// proxy's own path disables three of these for the same reasons. Exact set equality on purpose: a name
	/// dropped here is a component that runs again, and a name added is a decision this list has not seen.
	/// </summary>
	private static readonly string[] WorldWriters =
	[
		"WaterContainerItem", "CustomItemBehaviour", "LightItem", "WatchScript", "EPdaScript",
		"GrapplingHook", "GunScript", "GeigerCounterAudio", "AutoPump",
	];

	/// <summary>The sites that must retire a standing object before they decide over the same id: the drop, the destroy report, the trap-drop presentation, the cook replay's duplicate check and the reconcile's landing.</summary>
	private const int MinimumRetireSites = 5;

	[Fact]
	public void TheRecipe_TurnsOffEveryWorldFacingSurface()
	{
		var body = RequireMethodBody(RecipeFile, "StandingItemRecipe", "MakeInert");

		Assert.True(
			SetsFalse(body, "simulated"),
			$"{RecipeFile}: `MakeInert` must set `rb.simulated = false` — the game's own \"not in the world\" switch, and the one §6.2 names");
		Assert.True(
			Reads(body, "Collider2D") && Reads(body, "Renderer"),
			$"{RecipeFile}: `MakeInert` must take every `Collider2D` and every `Renderer` out — the collider is what makes a parked copy clickable, draggable and a valid recipe ingredient (§4), the renderer is what makes it visible, and an item prefab may present itself through a line, a trail or a particle renderer as well as a sprite");
		Assert.True(
			Reads(body, "Light2DTypeName"),
			$"{RecipeFile}: `MakeInert` must switch the `Light2D` itself off — disabling `LightItem`'s Update only freezes the light at whatever the prefab authored, which for a torch is ON");
		Assert.True(
			RepositoryPaths.ReadText(RecipeFile).Contains("\"Light2D\"", StringComparison.Ordinal),
			$"{RecipeFile}: the light is matched by type NAME (`Light2D` lives in the URP runtime assembly, which this project does not reference) — the constant's value must stay the real type name");
		Assert.True(
			EnabledOffCount(body) >= 3,
			$"{RecipeFile}: `MakeInert` must disable at least the collider, the renderer and the light (`X.enabled = false`) — found {EnabledOffCount(body)}");
	}

	[Fact]
	public void TheRecipe_DisablesExactlyTheComponentsThatActOnOwnerLocalState()
	{
		var body = RequireMethodBody(RecipeFile, "StandingItemRecipe", "MakeInert");
		var expected = WorldWriters.OrderBy(name => name, StringComparer.Ordinal).ToArray();
		var actual = DisabledTypes(body).OrderBy(name => name, StringComparer.Ordinal).ToArray();

		Assert.Equal(expected, actual);
	}

	[Fact]
	public void TheRecipe_BuildsTheObjectInTheFrameItIsCreated()
	{
		var body = RequireMethodBody(RecipeFile, "StandingItemRecipe", "Build");

		Assert.True(
			Reads(body, "ItemInstanceId") && Reads(body, "StandingItemObject"),
			$"{RecipeFile}: `Build` must attach the instance id AND the category marker in the frame the object is created — before `Item.Start` runs, which is what keeps `ItemWorldSync.OnItemInstantiated` from allocating a fresh id for it (§3)");
		Assert.True(
			AssignsMember(body, "Id"),
			$"{RecipeFile}: the attached instance id must also be GIVEN its value (`.Id = …`) — a component that carries no id classifies nothing and lets `Item.Start`'s hook allocate a fresh one for the object");
		Assert.True(
			Reads(body, "MakeInert"),
			$"{RecipeFile}: `Build` must run the inert recipe on every object it creates — an object that skips it is a live, clickable, visible copy of another player's item");
		Assert.False(
			Reads(body, "FreshItemDrop"),
			$"{RecipeFile}: `Build` must never add `FreshItemDrop` — §2's \"safe by omission\" row: the glowing floating pickup effect is a creation-flow presentation the game adds on its own flows, not on a copy of somebody's carried item");
	}

	[Fact]
	public void TheRecipe_NeverLoadsIntoARealContainer()
	{
		var source = RepositoryPaths.ReadText(RecipeFile);

		Assert.False(
			Invokes(source, "LoadItem"),
			$"{RecipeFile}: the recipe must never call `Container.LoadItem` — §6 constraint 5's first shape: it refuses a child that already holds weight and refuses outright inside a nested container, each refusal a player-facing `PlayerCamera.main.DoAlert`, and hand-attaching children instead would let `ContainerBroke`/`UnloadAllItems` spill standing children into the local world as live items");
		Assert.False(
			ReadsARealContainer(source),
			$"{RecipeFile}: the standing tree must not reach for a real `Container` component at all — the data, not the scene hierarchy, is its tree");
	}

	[Fact]
	public void TheMaterializer_ConsumesTheCarriedFactEdgeAndTheOwnerLeaves()
	{
		var bind = RequireMethodBody(MaterializerFile, "StandingItemMaterializer", "BindToSession");
		var reset = RequireMethodBody(MaterializerFile, "StandingItemMaterializer", "ResetSessionState");

		foreach (var (member, why) in new[]
		{
			("CloneSnapshotUpdated", "the carried-fact edge is where every carried row arrives (§6.1) — the 1 Hz snapshot, the single fact, the starting supplies and the drop all fire it, and so do the enemy/medical/limb refreshes, so the reconcile has to stay cheap"),
			("RemoteSceneChanged", "an owner that left the world carries nothing this side must incarnate"),
		})
		{
			Assert.True(SubscribesTo(bind, member), $"{MaterializerFile}: `BindToSession` must SUBSCRIBE `{member}` (`+=`) — {why}");
		}

		Assert.True(
			Invokes(reset, "RetireObject") && Reads(reset, "Object"),
			$"{MaterializerFile}: `ResetSessionState` must retire every standing object and drop the holder — CUO owns this lifetime, the game's own layer clear cannot (§6.6)");

		// The session-end edge itself is the session binding's (the composition root every other domain's
		// teardown hangs off), so the binding — not a fifth subscription — is what has to call the reset; and
		// the adapter teardown must do it too, because these are scene objects this side owns.
		var teardown = RequireMethodBody(BindingFile, "GameAdapterSessionBinding", "OnSessionEnded");
		Assert.True(
			teardown.Contains("StandingMaterializer.ResetSessionState()", StringComparison.Ordinal),
			$"{BindingFile}: the session teardown must retire the standing objects — an object that outlives the world dereferences `WorldGeneration.world` in `Item.Update` every frame (batch `20261002-h`)");

		var dispose = RequireMethodBody(AdapterFile, "GameAdapter", "Dispose");
		Assert.True(
			dispose.Contains("StandingMaterializer.ResetSessionState()", StringComparison.Ordinal),
			$"{AdapterFile}: the adapter teardown must retire them as well, beside the clones it already destroys — a plugin unload must not leave standing objects in the scene");
	}

	[Fact]
	public void TheMaterializer_GatesOnALiveWorldAndAnOwnerInTheWorld()
	{
		var body = RequireMethodBody(MaterializerFile, "StandingItemMaterializer", "OnOwnerFactsChanged");

		Assert.True(
			GuardsOn(body, "HasWorld"),
			$"{MaterializerFile}: the reconcile must run under a NEGATED `HarmonyTraverse.HasWorld` — §6.7's gate, the same one the world materialization uses, because `Item.Update` dereferences the world every frame");
		Assert.True(
			GuardsOn(body, "IsRemoteInWorld"),
			$"{MaterializerFile}: the reconcile must refuse an owner that is not in the world — its rows are the last thing it reported, and materializing them would park objects for an absent member");
		Assert.True(
			Reads(body, "StandingItemPlan"),
			$"{MaterializerFile}: the wanted set must come from `StandingItemPlan.Build` — the data decides what exists, and that decision is pinned as a pure rule beside this");
	}

	[Fact]
	public void TheMaterializer_ZeroesTheInstanceIdBeforeItDestroysTheObject()
	{
		var body = RequireMethodBody(MaterializerFile, "StandingItemMaterializer", "RetireObject");

		Assert.True(
			ZeroesIdBeforeDestroy(body),
			$"{MaterializerFile}: `RetireObject` must zero the instance id BEFORE it destroys the object — `Object.Destroy` is deferred to the end of the frame, and the zeroed id is what makes the world path's own idempotency lookups (`FindWorldItem`, the adopt scan's \"already synced\" clause) answer \"absent\" in the same frame, which is what lets a drop retire the incarnation and materialize the world copy in one pass");
	}

	[Fact]
	public void TheWorldMaterialization_RetiresAStandingIncarnationBeforeItsIdempotencyCheck()
	{
		var body = RequireMethodBody(SceneOpsFile, "RemoteItemSceneOps", "SpawnWorldItem");

		Assert.True(
			RetiresBefore(body, "FindWorldItem"),
			$"{SceneOpsFile}: `SpawnWorldItem` is the ONE entry every world materialization goes through (the spawn event, the drop, the cook replay, the landing, the restored-cut reconcile), so it must retire a standing incarnation of the row BEFORE its own \"already present locally\" check — otherwise that guard reads the invisible, unsimulated object as the world copy and no world item is ever materialized for the id");
	}

	[Fact]
	public void TheWorldTransition_RetiresTheStandingObjectAtEverySite()
	{
		var failures = new List<string>();
		var sites = 0;
		foreach (var (file, type, method, why) in new[]
		{
			(ApplicationFile, "ItemApplication", "OnRemoteItemDropped", "the row left an inventory into the world: the carried incarnation must go so the ordinary world path materializes a proper copy (re-placing it would drive an invisible, non-colliding, unsimulated object with the position stream)"),
			(ApplicationFile, "ItemApplication", "OnRemoteItemDestroyed", "the destroy report is the data saying that carried row is gone — retire it now rather than up to a second later on the next snapshot"),
			(ApplicationFile, "ItemApplication", "ApplyTrapDropPresentation", "this path branches before the materialization funnel: a standing object on the drop's id would answer its own lookup, and the drop would be skipped as \"already left the world\" instead of materializing the row"),
			(CookReplayFile, "ItemCookReplayApplier", "OnRemoteItemCooked", "the same shape one call site over: its duplicate check would read a standing object as the cooked item, and the world row would never be materialized"),
			(ReconcileFile, "ItemReconcile", "Land", "a world row whose id is still incarnated as a carried row: the incarnation is retired and the row materializes here"),
		})
		{
			var body = RequireMethodBody(file, type, method);
			if (!Invokes(body, "Retire"))
			{
				failures.Add($"{file} ({type}.{method}): no `…Retire(…)` call — {why}");
				continue;
			}

			if (AsksTheCategory(body))
			{
				failures.Add($"{file} ({type}.{method}): asks `StandingItems.Is(…)` — these sites must ask the MARKER (`Retire`, which is marker-driven): by the time they run the data may already hold the id as a world row, so the category answers false while the object still carries every switch of the recipe");
				continue;
			}

			sites++;
		}

		Assert.True(failures.Count == 0, "standing-item transition gate failed" + Environment.NewLine + string.Join(Environment.NewLine, failures));
		Assert.True(
			sites >= MinimumRetireSites,
			$"found {sites} standing-retire site(s) — the census floor is {MinimumRetireSites}, so a transition site (or the retire seam's name) changed shape");
	}

	[Theory]
	[InlineData("Disable<LightItem>(obj);", "LightItem", true)]
	[InlineData("Disable<WaterContainerItem>(obj);", "WaterContainerItem", true)]
	[InlineData("// Disable<LightItem>(obj);", "LightItem", false)]
	[InlineData("\"Disable<LightItem>(obj);\"", "LightItem", false)]
	[InlineData("Disable(lightItem);", "LightItem", false)]
	public void TheDisabledComponentMatcher_ReadsGenericCallsAndIgnoresMentions(string body, string typeName, bool expected) =>
		Assert.Equal(expected, DisabledTypes(Sample(body)).Contains(typeName));

	[Theory]
	[InlineData("_standing.Retire(itemId, \"reason\");", true)]
	[InlineData("if (_standing.Retire(itemId, \"reason\")) { }", true)]
	[InlineData("// _standing.Retire(itemId, \"reason\");", false)]
	[InlineData("var retired = _objects.Count;", false)]
	[InlineData("\"_standing.Retire(itemId)\"", false)]
	public void TheRetireMatcher_ReadsCallsAndIgnoresMentions(string body, bool expected) =>
		Assert.Equal(expected, Invokes(Sample(body), "Retire"));

	[Theory]
	[InlineData("if (StandingItems.Is(item)) { }", true)]
	[InlineData("var standing = StandingItems.Is(place.Item!);", true)]
	[InlineData("// StandingItems.Is(item)", false)]
	[InlineData("IsStanding(item)", false)]
	[InlineData("\"StandingItems.Is(item)\"", false)]
	public void TheCategoryAskerMatcher_ReadsCallsAndIgnoresMentions(string body, bool expected) =>
		Assert.Equal(expected, AsksTheCategory(Sample(body)));

	[Theory]
	[InlineData("_facts.CloneSnapshotUpdated += OnOwnerFactsChanged;", "CloneSnapshotUpdated", true)]
	[InlineData("_facts.CloneSnapshotUpdated -= OnOwnerFactsChanged;", "CloneSnapshotUpdated", false)]
	[InlineData("// _facts.CloneSnapshotUpdated += OnOwnerFactsChanged;", "CloneSnapshotUpdated", false)]
	public void TheSubscriptionMatcher_ReadsTheAddHalfOnly(string body, string member, bool expected) =>
		Assert.Equal(expected, SubscribesTo(Sample(body), member));

	[Theory]
	[InlineData("if (!HarmonyTraverse.HasWorld) { return; }", "HasWorld", true)]
	[InlineData("if (HarmonyTraverse.HasWorld) { }", "HasWorld", false)]
	[InlineData("if (!_session.IsRemoteInWorld(owner)) { return; }", "IsRemoteInWorld", true)]
	[InlineData("if (_session.IsRemoteInWorld(owner)) { }", "IsRemoteInWorld", false)]
	[InlineData("// if (!HarmonyTraverse.HasWorld) { }", "HasWorld", false)]
	public void TheGuardMatcher_ReadsNegatedGuardsOnly(string body, string member, bool expected) =>
		Assert.Equal(expected, GuardsOn(Sample(body), member));

	[Theory]
	[InlineData("idComp.Id = 0; Object.Destroy(item.gameObject);", true)]
	[InlineData("Object.Destroy(item.gameObject); idComp.Id = 0;", false)]
	[InlineData("Object.Destroy(item.gameObject);", false)]
	public void TheZeroThenDestroyMatcher_ReadsTheOrder(string body, bool expected) =>
		Assert.Equal(expected, ZeroesIdBeforeDestroy(Sample(body)));

	[Theory]
	[InlineData("_standing.Retire(w.ItemId, \"r\"); if (FindWorldItem(w.ItemId) != null) { return; }", true)]
	[InlineData("if (FindWorldItem(w.ItemId) != null) { return; } _standing.Retire(w.ItemId, \"r\");", false)]
	[InlineData("if (FindWorldItem(w.ItemId) != null) { return; }", false)]
	public void TheFunnelOrderMatcher_ReadsWhichCallComesFirst(string body, bool expected) =>
		Assert.Equal(expected, RetiresBefore(Sample(body), "FindWorldItem"));

	[Theory]
	[InlineData("obj.AddComponent<ItemInstanceId>().Id = itemId;", "Id", true)]
	[InlineData("obj.AddComponent<ItemInstanceId>();", "Id", false)]
	public void TheAssignmentMatcher_ReadsWritesAndIgnoresMentions(string body, string member, bool expected) =>
		Assert.Equal(expected, AssignsMember(Sample(body), member));

	[Theory]
	[InlineData("parent.GetComponent<Container>().LoadItem(item);", true, true)]
	[InlineData("obj.transform.SetParent(holder.transform);", false, false)]
	[InlineData("// parent.LoadItem(item);", false, false)]
	public void TheContainerLoadMatcher_ReadsCallsAndIgnoresMentions(string body, bool loads, bool readsContainer) =>
		Assert.Equal((loads, readsContainer), (Invokes(Sample(body), "LoadItem"), ReadsARealContainer(Sample(body))));

	[Theory]
	[InlineData("rb.simulated = false;", "simulated", true)]
	[InlineData("rb.simulated = true;", "simulated", false)]
	[InlineData("// rb.simulated = false;", "simulated", false)]
	[InlineData("col.enabled = false;", "enabled", true)]
	[InlineData("sr.enabled = false;", "enabled", true)]
	public void TheSwitchMatcher_ReadsAssignmentsAndIgnoresMentions(string body, string member, bool expected) =>
		Assert.Equal(expected, SetsFalse(Sample(body), member));

	/// <summary>The type arguments of every `Disable&lt;T&gt;(…)` invocation in a body.</summary>
	private static HashSet<string> DisabledTypes(string body) =>
		[.. Parse(body).DescendantNodes()
			.OfType<InvocationExpressionSyntax>()
			.Where(invocation => invocation.Expression is GenericNameSyntax generic
				&& generic.Identifier.ValueText == "Disable")
			.SelectMany(invocation => ((GenericNameSyntax)invocation.Expression).TypeArgumentList.Arguments
				.OfType<IdentifierNameSyntax>())
			.Select(argument => argument.Identifier.ValueText)];

	/// <summary>How many `X.enabled = false` assignments a body makes.</summary>
	private static int EnabledOffCount(string body) =>
		Parse(body).DescendantNodes()
			.OfType<AssignmentExpressionSyntax>()
			.Count(assignment => assignment.Left is MemberAccessExpressionSyntax access
				&& access.Name.Identifier.ValueText == "enabled"
				&& assignment.Right.IsKind(SyntaxKind.FalseLiteralExpression));

	/// <summary>Whether a body assigns `false` to the named member.</summary>
	private static bool SetsFalse(string body, string member) =>
		Parse(body).DescendantNodes()
			.OfType<AssignmentExpressionSyntax>()
			.Any(assignment => assignment.Left is MemberAccessExpressionSyntax access
				&& access.Name.Identifier.ValueText == member
				&& assignment.Right.IsKind(SyntaxKind.FalseLiteralExpression));

	/// <summary>Whether a body writes the named member at all (`x.Id = …`).</summary>
	private static bool AssignsMember(string body, string member) =>
		Parse(body).DescendantNodes()
			.OfType<AssignmentExpressionSyntax>()
			.Any(assignment => assignment.Left is MemberAccessExpressionSyntax access
				&& access.Name.Identifier.ValueText == member);

	/// <summary>Whether a body SUBSCRIBES the named member — the `+=` half only, so an inversion to `-=` fails.</summary>
	private static bool SubscribesTo(string body, string member) =>
		Parse(body).DescendantNodes()
			.OfType<AssignmentExpressionSyntax>()
			.Any(assignment => assignment.IsKind(SyntaxKind.AddAssignmentExpression)
				&& assignment.Left is MemberAccessExpressionSyntax access
				&& access.Name.Identifier.ValueText == member);

	/// <summary>Whether a body GUARDS on the named member with a negation — `!X.Member`, so a removed or un-negated guard fails.</summary>
	private static bool GuardsOn(string body, string member) =>
		Parse(body).DescendantNodes()
			.OfType<PrefixUnaryExpressionSyntax>()
			.Any(negation => negation.IsKind(SyntaxKind.LogicalNotExpression)
				&& negation.DescendantNodes().OfType<IdentifierNameSyntax>()
					.Any(name => string.Equals(name.Identifier.ValueText, member, StringComparison.Ordinal)));

	/// <summary>Whether a body calls the named method — by call, so a comment or a string literal is not a site.</summary>
	private static bool Invokes(string body, string method) =>
		Parse(body).DescendantNodes()
			.OfType<InvocationExpressionSyntax>()
			.Any(invocation => IsCallTo(invocation, method));

	/// <summary>
	/// A call by name, in both shapes the parser produces: unqualified (`FindWorldItem(id)`) and through a
	/// member (`_standing.Retire(id)`, `Object.Destroy(obj)`) — including a generic member
	/// (`obj.AddComponent&lt;T&gt;()` becomes a member access whose NAME is the generic name).
	/// </summary>
	private static bool IsCallTo(InvocationExpressionSyntax invocation, string method) =>
		invocation.Expression switch
		{
			MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText == method,
			GenericNameSyntax generic => generic.Identifier.ValueText == method,
			IdentifierNameSyntax identifier => identifier.Identifier.ValueText == method,
			_ => false,
		};

	/// <summary>Whether a body reads a real `Container` component (`GetComponent&lt;Container&gt;` / `GetComponentsInChildren&lt;Container&gt;`).</summary>
	private static bool ReadsARealContainer(string body) =>
		Parse(body).DescendantNodes()
			.OfType<InvocationExpressionSyntax>()
			.Select(invocation => invocation.Expression switch
			{
				GenericNameSyntax generic => generic,
				MemberAccessExpressionSyntax { Name: GenericNameSyntax member } => member,
				_ => null,
			})
			.Any(generic => generic is not null
				&& generic.Identifier.ValueText.StartsWith("GetComponent", StringComparison.Ordinal)
				&& generic.TypeArgumentList.Arguments.OfType<IdentifierNameSyntax>()
					.Any(argument => argument.Identifier.ValueText == "Container"));

	/// <summary>Whether a body zeroes an instance id before it calls `Destroy` — the ordering the world path's same-frame idempotency depends on.</summary>
	private static bool ZeroesIdBeforeDestroy(string body)
	{
		var zero = FirstPosition(body, node =>
			node is AssignmentExpressionSyntax assignment
			&& assignment.Left is MemberAccessExpressionSyntax access
			&& access.Name.Identifier.ValueText == "Id");
		var destroy = FirstPosition(body, node =>
			node is InvocationExpressionSyntax invocation && IsCallTo(invocation, "Destroy"));

		return zero is { } zeroed && destroy is { } destroyed && zeroed < destroyed;
	}

	/// <summary>Whether the `Retire` call comes before the other named call in a body.</summary>
	private static bool RetiresBefore(string body, string laterMethod)
	{
		var retire = FirstPosition(body, node =>
			node is InvocationExpressionSyntax invocation && IsCallTo(invocation, "Retire"));
		var later = FirstPosition(body, node =>
			node is InvocationExpressionSyntax invocation && IsCallTo(invocation, laterMethod));

		return retire is { } first && later is { } second && first < second;
	}

	/// <summary>The earliest position of a node matching a predicate in a body.</summary>
	private static int? FirstPosition(string body, Func<SyntaxNode, bool> match) =>
		Parse(body).DescendantNodes()
			.Where(match)
			.Select(node => (int?)node.SpanStart)
			.FirstOrDefault();

	/// <summary>Whether a body asks the CATEGORY (`StandingItems.Is`) — the question these sites must not have.</summary>
	private static bool AsksTheCategory(string body) =>
		Parse(body).DescendantNodes()
			.OfType<InvocationExpressionSyntax>()
			.Any(invocation => invocation.Expression is MemberAccessExpressionSyntax access
				&& access.Name.Identifier.ValueText == "Is"
				&& access.Expression is IdentifierNameSyntax owner
				&& owner.Identifier.ValueText == "StandingItems");

	/// <summary>True when a body reads the named identifier — Roslyn, so a comment or a string literal that mentions it is not a site.</summary>
	private static bool Reads(string body, string identifier) =>
		Parse(body).DescendantNodes()
			.OfType<IdentifierNameSyntax>()
			.Any(name => string.Equals(name.Identifier.ValueText, identifier, StringComparison.Ordinal));

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

	/// <summary>A one-method sample type, so the matcher self-tests run against real syntax rather than raw text.</summary>
	private static string Sample(string body) => $"internal sealed class Sample {{ private void M() {{ {body} }} }}";

	private static SyntaxNode Parse(string source) =>
		CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview)).GetRoot();
}
