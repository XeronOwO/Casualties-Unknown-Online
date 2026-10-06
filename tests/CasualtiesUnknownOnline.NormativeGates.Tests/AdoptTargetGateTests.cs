using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Tooling.NormativeGates;

/// <summary>
/// The adopt-target gate (ticket <c>backlog/done/second-drop-report-loses-its-world-object.md</c>, batch
/// `20261006-h`, rows 3 and 4 — both failed 2/2 on the operator and on the third peer).
///
/// <para>
/// The materialization adopt scan (<c>RemoteItemSceneOps.FindExistingAt</c>) is how the item domain claims a
/// scene object as an authority row's copy instead of materializing a duplicate. Batch `20261006-h`'s second
/// child was claimed from a REMOTE-CLONE DISPLAY PROXY: `CloneInventoryRenderer.RestoreRemoteContents`
/// retires a stale proxy with `container.UnloadItem(old)` — which detaches it (`transform.SetParent(null)`,
/// so <c>ItemWorldSync.IsWorldItem</c> turns true) and moves it <c>Vector3.up * 1.5f</c>, exactly onto
/// <c>AdoptTolerance</c> — then deactivates it and queues its deferred destroy. The scan's only proxy test was
/// <c>GetComponentInParent&lt;RemoteCloneRender&gt;()</c>, whose default <c>includeInactive: false</c> cannot
/// see the marker on an object the renderer has just deactivated, so the proxy passed as a generation-time
/// world item. The id was stamped on an object Unity destroyed 10-30 ms later; the destroy carried the id, so
/// it was REPORTED (unlike <c>KillRemoteItem</c>, which zeroes ids first), the host's kernel moved the item to
/// <c>terminal</c>, and the third peer's own destroy was refused <c>InvalidTransition</c> — one sent and
/// committed departure with no standing object anywhere.
/// </para>
///
/// <para>
/// The rule this gate encodes: an authority id may only be stamped onto a LIVE, non-proxy, still-unsynced
/// world object of the row's definition within tolerance — the named tie-break is
/// <c>AdoptTargetRule.Allows</c>, whose truth table is pinned as a pure rule by <c>AdoptTargetRuleTests</c>.
/// A display proxy is never an item-domain object at all, so every classifier that treats "an id-less
/// standalone world item" as an authority candidate — the adopt scan, the reconcile's late-local sweep, the
/// generation publish, the restored-cut leftover sweep — asks the same one predicate.
/// </para>
///
/// <para>
/// Reach, stated rather than implied: the scan surface is the four classifier bodies, the three domain-path
/// bodies (`FindWorldItem`, `OnItemDestroyed`, `BindToContainer`), the rule's own body and the display-proxy
/// predicate's body, each resolved through Roslyn; the census floors (four classifiers, six domain-path tests)
/// make a renamed type or an emptied scan fail loudly instead of checking nothing. The matchers are pinned with
/// positive and negative samples. What is NOT reached: whether the runtime landing is right — a scene scan
/// needs the game's own objects, so the three-client acceptance run reads the row (both ids in the host's world
/// table, both materialized on the third peer, no <c>terminal</c> entry, no <c>InvalidTransition</c>), and the
/// rule's behaviour on real objects is that run's, not this gate's. The one classifier in this family the gate
/// does NOT cover is <c>ItemWorldSync.OnItemInstantiated</c>, which takes a root proxy that was never loaded
/// into the item domain at its own <c>Start</c>; it is filed with its own fixture as
/// <c>docs/backlog/todo/nested-container-clone-proxy-leaks-as-world-item.md</c> rather than half-fixed here.
/// </para>
/// </summary>
public class AdoptTargetGateTests
{
	private const string RemoteItemSceneOpsFile = "src/CasualtiesUnknownOnline.GameAdapter/Items/RemoteItemSceneOps.cs";

	private const string AdoptTargetRuleFile = "src/CasualtiesUnknownOnline.GameAdapter/Items/AdoptTargetRule.cs";

	private const string ItemWorldSyncFile = "src/CasualtiesUnknownOnline.GameAdapter/Items/ItemWorldSync.cs";

	private const string ItemReconcileFile = "src/CasualtiesUnknownOnline.GameAdapter/Items/ItemReconcile.cs";

	private const string GeneratedItemAuthorityFile = "src/CasualtiesUnknownOnline.GameAdapter/WorldGen/GeneratedItemAuthority.cs";

	private const string GeneratedItemReconcileFile = "src/CasualtiesUnknownOnline.GameAdapter/WorldGen/GeneratedItemReconcile.cs";

	/// <summary>The named tie-break every adopt candidate is routed through.</summary>
	private const string RuleCall = "AdoptTargetRule.Allows(";

	/// <summary>The fact the scan must supply from the shared predicate — not from an ancestor lookup of its own.</summary>
	private const string ProxyFact = "displayProxy: ItemWorldSync.IsDisplayProxy(item)";

	/// <summary>The other fact the scan must supply: the scene has not already retired the object.</summary>
	private const string RetiredFact = "retired:";

	/// <summary>The shared predicate's two markers and its inactive-covering ancestor scan.</summary>
	private const string OwnMarker = "RemoteInventoryItemId";

	private const string TreeMarker = "RemoteCloneRender";

	/// <summary>The classifier census floor: one proxy test per classifier that may claim an id-less world object.</summary>
	private const int MinimumProxyTestSites = 4;

	[Fact]
	public void TheAdoptScan_RoutesEveryCandidateThroughTheNamedRule()
	{
		var body = RequireMethodBody(RemoteItemSceneOpsFile, "RemoteItemSceneOps", "FindExistingAt");

		Assert.True(
			RoutesThroughTheRule(body),
			$"{RemoteItemSceneOpsFile}: `FindExistingAt` must route every candidate through `{RuleCall}…` and hand it `displayProxy:` and `retired:` — the scan's guard list is the adopt tie-break, and a rule the scan does not call cannot refuse the display proxy that cost batch `20261006-h` its second child");
		Assert.True(
			ProxyTests(body) >= 1,
			$"{RemoteItemSceneOpsFile}: `FindExistingAt` must pass `{ProxyFact}` — the proxy fact comes from ONE shared predicate, because an ancestor lookup of the scan's own (`GetComponentInParent<{TreeMarker}>()`) cannot see a marker on an object the clone renderer has already deactivated");
		Assert.True(
			ReadsClause(body, "retired"),
			$"{RemoteItemSceneOpsFile}: `FindExistingAt` must pass a `{RetiredFact}…` fact — a retired object (the renderer deactivates a stale proxy before its deferred destroy) must never be stamped with an authority id: the stamp outlives the object and comes back as a destroy report");
	}

	[Fact]
	public void TheRule_RefusesDisplayProxiesAndRetiredObjects()
	{
		var body = RequireMethodBody(AdoptTargetRuleFile, "AdoptTargetRule", "Allows");

		foreach (var (clause, why) in new[]
		{
			("sameDefinition", "an object of another definition is not this row's copy"),
			("alreadySynced", "an object that already carries an id belongs to another row"),
			("displayProxy", "a clone display proxy is presentation only — it is never an authority item"),
			("retired", "an object the scene has already retired must not be given an id that outlives it"),
			("worldItem", "an object inside an inventory or on a limb is character state, not a world item"),
			("tutorialProp", "a per-player course prop must never be bound to a shared item"),
			("withinTolerance", "a copy beyond the tolerance is not the row's copy"),
		})
		{
			Assert.True(
				ReadsClause(body, clause),
				$"{AdoptTargetRuleFile}: `AdoptTargetRule.Allows` must decide on `{clause}` — {why}. The rule is the ONE place the tie-break lives; a clause dropped here is a case the scan silently stops refusing");
		}
	}

	[Fact]
	public void TheDisplayProxyPredicate_ReadsBothMarkersAndCoversInactiveObjects()
	{
		var body = RequireMethodBody(ItemWorldSyncFile, "ItemWorldSync", "IsDisplayProxy");

		Assert.True(
			ReadsClause(body, OwnMarker),
			$"{ItemWorldSyncFile}: `IsDisplayProxy` must read the proxy's OWN marker (`{OwnMarker}`) — the clone renderer writes it onto the proxy itself, so it survives the detach and the deactivation that hide the tree marker");
		Assert.True(
			ReadsClause(body, TreeMarker) && ReadsClause(body, "includeInactive"),
			$"{ItemWorldSyncFile}: `IsDisplayProxy` must also read the clone-tree marker (`{TreeMarker}`) through the `includeInactive` overload — without the inactive coverage the predicate repeats the exact hole that let batch `20261006-h` adopt a retired proxy");
	}

	[Fact]
	public void EveryClassifierThatClaimsAnIdLessWorldItem_SkipsDisplayProxies()
	{
		var failures = new List<string>();
		var sites = 0;

		foreach (var (file, type, method) in Classifiers)
		{
			var body = RequireMethodBody(file, type, method);
			if (!body.Contains("IsDisplayProxy(", StringComparison.Ordinal))
			{
				failures.Add($"{file} ({type}.{method}): no `IsDisplayProxy(…)` test — this classifier decides over id-less standalone world objects, and a detached display proxy looks exactly like one (the batch's second child was claimed that way)");
				continue;
			}

			sites++;
		}

		Assert.True(failures.Count == 0, "adopt-target proxy-family gate failed" + Environment.NewLine + string.Join(Environment.NewLine, failures));
		Assert.True(
			sites >= MinimumProxyTestSites,
			$"found {sites} display-proxy test(s) across the id-less-world-item classifiers — the census floor is {MinimumProxyTestSites}, so the scan surface (or the predicate's name) changed shape");
	}

	[Fact]
	public void EveryDomainPathThatCouldAddressAProxy_AsksTheProviderPredicate()
	{
		var failures = new List<string>();
		var sites = 0;

		foreach (var (file, type, method, minimum, why) in DomainPaths)
		{
			var body = RequireMethodBody(file, type, method);
			var found = ProxyTests(body);
			if (found < minimum)
			{
				failures.Add($"{file} ({type}.{method}): {found} `IsDisplayProxy(…)` test(s), the pin wants at least {minimum} — {why}");
				continue;
			}

			sites += found;
		}

		Assert.True(failures.Count == 0, "adopt-target domain-path gate failed" + Environment.NewLine + string.Join(Environment.NewLine, failures));
		Assert.True(
			sites >= MinimumDomainPathSites,
			$"found {sites} display-proxy test(s) across the domain paths — the census floor is {MinimumDomainPathSites}, so the scan surface (or the predicate's name) changed shape");
	}

	[Theory]
	[InlineData("var existing = FindExistingAt(w.Pos, w.Item.ItemId);", 0)]
	[InlineData("if (ItemWorldSync.IsDisplayProxy(item)) { continue; }", 1)]
	[InlineData("if (IsDisplayProxy(candidate)) { }", 1)]
	[InlineData("if (notIsDisplayProxy(candidate)) { }", 0)]
	[InlineData("// ItemWorldSync.IsDisplayProxy(item)", 0)]
	[InlineData("\"ItemWorldSync.IsDisplayProxy(item)\"", 0)]
	public void TheProxyTestMatcher_ReadsCallsAndIgnoresMentions(string body, int expected) =>
		Assert.Equal(expected, ProxyTests($"internal sealed class Sample {{ private void M() {{ {body} }} }}"));

	[Theory]
	[InlineData("AdoptTargetRule.Allows(sameDefinition: item.id != itemId, displayProxy: ItemWorldSync.IsDisplayProxy(item), retired: !item.gameObject.activeInHierarchy)", true)]
	[InlineData("if (item.id != itemId || !ItemWorldSync.IsWorldItem(item)) { continue; }", false)]
	[InlineData("if (item.GetComponentInParent<RemoteCloneRender>() != null) { continue; }", false)]
	[InlineData("// AdoptTargetRule.Allows(displayProxy: x, retired: y)", false)]
	[InlineData("\"AdoptTargetRule.Allows(displayProxy: x, retired: y)\"", false)]
	[InlineData("AdoptTargetRule.Allows(sameDefinition: a, retired: b)", false)]
	public void TheAdoptScanMatcher_ReadsTheRuleCallAndTheFactsItIsGiven(string body, bool expected) =>
		Assert.Equal(expected, RoutesThroughTheRule(body));

	[Theory]
	[InlineData("return item.GetComponent<RemoteInventoryItemId>() != null || item.GetComponentInParent<RemoteCloneRender>(includeInactive: true) != null;", true)]
	[InlineData("return item.GetComponentInParent<RemoteCloneRender>() != null;", false)]
	[InlineData("return item.GetComponent<RemoteInventoryItemId>() != null;", false)]
	public void TheProxyPredicateMatcher_RequiresBothMarkersAndInactiveCoverage(string body, bool expected) =>
		Assert.Equal(expected, ReadsBothMarkers(body));

	[Theory]
	[InlineData("var allowed = !displayProxy;", "displayProxy", true)]
	[InlineData("Allows(displayProxy: item.IsProxy());", "displayProxy", true)]
	[InlineData("// displayProxy", "displayProxy", false)]
	[InlineData("/* retired */", "retired", false)]
	[InlineData("\"retired\"", "retired", false)]
	[InlineData("var retiredCopy = 1;", "retired", false)]
	public void TheClauseMatcher_ReadsIdentifiersAndIgnoresMentions(string body, string clause, bool expected) =>
		Assert.Equal(expected, ReadsClause($"internal sealed class Sample {{ private void M() {{ {body} }} }}", clause));

	/// <summary>
	/// The classifiers that decide over "an id-less standalone world item" and may therefore take authority over
	/// one: the materialization adopt scan (stamps an id), the reconcile's late-local sweep and the restored-cut
	/// leftover sweep (destroy), and the generation publish (allocates an id). Named rather than globbed — these
	/// are the sites the item domain reaches for a scene object it did not create.
	/// </summary>
	private static readonly (string File, string Type, string Method)[] Classifiers =
	[
		(RemoteItemSceneOpsFile, "RemoteItemSceneOps", "FindExistingAt"),
		(ItemReconcileFile, "ItemReconcile", "OnRemoteItemSnapshot"),
		(GeneratedItemAuthorityFile, "GeneratedItemAuthority", "Publish"),
		(GeneratedItemReconcileFile, "GeneratedItemReconcile", "Apply"),
	];

	/// <summary>
	/// The domain paths a display proxy must never reach, and how many predicate tests each declares. This is the
	/// destroy/report half of the same rule the classifiers carry: a proxy the clone renderer has RETIRED is
	/// inactive, so every ancestor lookup of the tree marker answered nothing for it — which is how the batch's
	/// adopted proxy reported its own destroy through `OnItemDestroyed` and how `FindWorldItem` would have handed
	/// it back to a domain operation. `BindToContainer` is the stamp-one-call-over: its position scan fills in a
	/// generation-time container's id, and a clone container proxy is an id-less `Container` like any other.
	/// Deliberately NOT listed: the input-path tests (`RemoteCloneContainerGuard`, `RemoteDragProxyQuery`, the
	/// drag/backpack query guards, the release-path predicates) — they run on a user's click or drag, and an
	/// inactive object cannot be clicked, so the inactive half of the hole cannot open there; they keep their
	/// own marker tests (named here so the omission is a decision, not an oversight).
	/// </summary>
	private static readonly (string File, string Type, string Method, int Minimum, string Why)[] DomainPaths =
	[
		(RemoteItemSceneOpsFile, "RemoteItemSceneOps", "FindWorldItem", 3, "an id-bearing proxy must never be answered as the domain object — one test per lookup path (the id index and both scene scans)"),
		(ItemWorldSyncFile, "ItemWorldSync", "OnItemDestroyed", 1, "a display proxy's destroy is not a player operation, and the renderer deactivates a stale proxy before destroying it"),
		(RemoteItemSceneOpsFile, "RemoteItemSceneOps", "BindToContainer", 2, "the container stamped here must be a generation-time object, never a clone container proxy (one test per candidate loop)"),
	];

	/// <summary>The domain-path census floor: three in `FindWorldItem`, one in `OnItemDestroyed`, two in `BindToContainer`.</summary>
	private const int MinimumDomainPathSites = 6;

	/// <summary>Display-proxy tests in a source — matched by CALL, so a comment or a string literal is not a site; the unqualified form counts too, because the predicate's own type calls it that way.</summary>
	private static int ProxyTests(string source) =>
		Parse(source).DescendantNodes()
			.OfType<InvocationExpressionSyntax>()
			.Count(invocation => invocation.Expression switch
			{
				MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText == "IsDisplayProxy",
				IdentifierNameSyntax identifier => identifier.Identifier.ValueText == "IsDisplayProxy",
				_ => false,
			});

	/// <summary>The adopt scan's rule: the tie-break call is asked, and it is handed both new facts — read through Roslyn, because a comment or a string literal that names the call is not a call.</summary>
	private static bool RoutesThroughTheRule(string body) =>
		Parse(body).DescendantNodes()
			.OfType<InvocationExpressionSyntax>()
			.Any(invocation => invocation.Expression is MemberAccessExpressionSyntax access
				&& access.Name.Identifier.ValueText == "Allows"
				&& access.Expression.ToString().EndsWith("AdoptTargetRule", StringComparison.Ordinal)
				&& invocation.ArgumentList.Arguments
					.Select(argument => argument.NameColon?.Name.Identifier.ValueText)
					.Where(name => name is not null)
					.ToHashSet(StringComparer.Ordinal)
					.IsSupersetOf(["displayProxy", "retired"]));

	/// <summary>The predicate's rule: both markers are read and the ancestor scan covers inactive objects.</summary>
	private static bool ReadsBothMarkers(string body) =>
		body.Contains(OwnMarker, StringComparison.Ordinal)
		&& body.Contains(TreeMarker, StringComparison.Ordinal)
		&& body.Contains("includeInactive: true", StringComparison.Ordinal);

	/// <summary>
	/// True when a body reads the named clause as an IDENTIFIER — a named argument, a parameter, a local. Read
	/// through Roslyn so a comment or a string literal that merely mentions the clause is not a clause: the
	/// guard this rule exists for was a call that LOOKED right and answered nothing.
	/// </summary>
	private static bool ReadsClause(string body, string clause) =>
		Parse(body).DescendantNodes()
			.OfType<IdentifierNameSyntax>()
			.Any(identifier => string.Equals(identifier.Identifier.ValueText, clause, StringComparison.Ordinal));

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
