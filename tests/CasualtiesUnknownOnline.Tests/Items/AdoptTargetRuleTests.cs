using System;
using System.Reflection;
using CasualtiesUnknownOnline.Tests.Patching;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Items;

/// <summary>
/// The materialization adopt scan's tie-break as a pure rule (ticket
/// <c>backlog/done/second-drop-report-loses-its-world-object.md</c>, batch `20261006-h`, rows 3 and 4):
/// whether a scene object may BE an authority row's copy instead of the row materializing one beside it.
///
/// <para>
/// The batch's second child was claimed from a REMOTE-CLONE DISPLAY PROXY. The clone renderer retires a
/// stale proxy by unloading it — <c>Container.UnloadItem</c> detaches it (so
/// <c>ItemWorldSync.IsWorldItem</c> turns true) and moves it <c>Vector3.up * 1.5f</c>, the scan's own
/// <c>AdoptTolerance</c> — then deactivates it and queues its end-of-frame destroy. Every clause the scan
/// had before this rule passes on that object, which is why the id was stamped onto it 10-30 ms before
/// Unity destroyed it: the destroy then carried the id, the host's kernel moved the item to
/// <c>terminal</c>, and the third peer's own destroy was refused <c>InvalidTransition</c>.
/// </para>
///
/// <para>
/// Reach: the rule's truth table only. The wiring that supplies the seven facts from a real scene object is
/// pinned by <c>AdoptTargetGateTests</c> (source shape), and whether a real drop row then lands is the
/// three-client acceptance run's row — a scan over <c>Item.allItems</c> needs the game's own objects.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public class AdoptTargetRuleTests
{
	private static readonly MethodInfo Allows = GameAssemblyHost.Adapter
		.GetType("CasualtiesUnknownOnline.GameAdapter.Items.AdoptTargetRule", throwOnError: true)!
		.GetMethod("Allows", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
		?? throw new InvalidOperationException("AdoptTargetRule.Allows not found.");

	[Fact]
	public void Allows_AcceptsALiveGenerationTimeWorldObject() =>
		Assert.True(Decide(), "a live, id-less, non-proxy world object of the row's definition within tolerance IS the row's copy — the adopt path exists for exactly this object");

	[Theory]
	[InlineData("sameDefinition")]
	[InlineData("alreadySynced")]
	[InlineData("displayProxy")]
	[InlineData("retired")]
	[InlineData("worldItem")]
	[InlineData("tutorialProp")]
	[InlineData("withinTolerance")]
	public void Allows_RefusesWhenAnySingleClauseFails(string clause) =>
		Assert.False(
			clause switch
			{
				"sameDefinition" => Decide(sameDefinition: false),
				"alreadySynced" => Decide(alreadySynced: true),
				"displayProxy" => Decide(displayProxy: true),
				"retired" => Decide(retired: true),
				"worldItem" => Decide(worldItem: false),
				"tutorialProp" => Decide(tutorialProp: true),
				"withinTolerance" => Decide(withinTolerance: false),
				_ => throw new ArgumentOutOfRangeException(nameof(clause), clause, "unknown clause"),
			},
			$"`{clause}` must refuse a candidate on its own — a clause that cannot is not part of the tie-break");

	[Fact]
	public void Allows_RefusesTheRetiredCloneProxyThatCostTheBatchItsSecondChild()
	{
		// The reading of batch 20261006-h, clause by clause. The proxy the renderer had just retired is
		// the row's definition, carries no domain id, sits exactly on the tolerance (the retire step's
		// 1.5-unit hop is the tolerance's own value) and its detached parent chain says "world item" —
		// every pre-existing clause ADMITS it. Only the two clauses this rule adds refuse it.
		Assert.True(
			Decide(sameDefinition: true, alreadySynced: false, displayProxy: false, retired: false, worldItem: true, tutorialProp: false, withinTolerance: true),
			"the retired proxy satisfies every clause the scan had before this rule — that is why the scan claimed it");
		Assert.False(Decide(displayProxy: true), "a clone display proxy is presentation only, whoever owns its lifetime");
		Assert.False(Decide(retired: true), "an id must never be stamped onto an object the scene has already taken out of play: the stamp outlives it and comes back as a destroy report");
	}

	private static bool Decide(
		bool sameDefinition = true,
		bool alreadySynced = false,
		bool displayProxy = false,
		bool retired = false,
		bool worldItem = true,
		bool tutorialProp = false,
		bool withinTolerance = true) =>
		(bool)Allows.Invoke(null, [sameDefinition, alreadySynced, displayProxy, retired, worldItem, tutorialProp, withinTolerance])!;
}
