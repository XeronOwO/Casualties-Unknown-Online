using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Tests.Patching;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Items;

/// <summary>
/// The standing-item plan: one owner's carried rows (the fact table's recursive snapshot) read as the set
/// of local objects the data asks for (ticket <c>mod-cross-player-solid-food-semantics</c>, §6 step 2).
/// The plan is the pure half of the materializer — what the DATA says should exist — and this pins the
/// rules that decide it, because the scene half only ever creates what this hands over.
///
/// <para>
/// Reach: the plan's decisions over a given carried tree. Whether a real session then creates, updates and
/// retires the objects is the acceptance batch's row (§7), and the switch recipe is pinned as source shape
/// by <c>StandingItemGateTests</c>.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public class StandingItemPlanTests
{
	private static readonly Type Plan = GameAssemblyHost.Adapter
		.GetType("CasualtiesUnknownOnline.GameAdapter.Items.StandingItemPlan", throwOnError: true)!;

	private static readonly MethodInfo Build = Plan
		.GetMethod("Build", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
		?? throw new InvalidOperationException("StandingItemPlan.Build not found.");

	private static readonly MethodInfo ContainsMethod = Method("Contains");
	private static readonly MethodInfo RowMethod = Method("Row");
	private static readonly MethodInfo ParentOfMethod = Method("ParentOf");

	[Fact]
	public void ThePlan_ListsEveryCarriedRowParentsFirst()
	{
		var plan = PlanOf(11, Row(101, "trashbag", Row(102, "waterbottle", Row(103, "bandage")), Row(104, "bread")));

		// Parents before children: the materializer creates objects in this order, so an object's data
		// parent always exists as a row by the time its child is built.
		Assert.Equal(new ulong[] { 101, 102, 103, 104 }, Ids(plan));
		Assert.Equal(4, Count(plan));
		Assert.Equal(11UL, Owner(plan));
		Assert.Equal(0UL, ParentOf(plan, 101)); // a top-level row: a slot or a limb home
		Assert.Equal(101UL, ParentOf(plan, 102));
		Assert.Equal(102UL, ParentOf(plan, 103));
		Assert.Equal(101UL, ParentOf(plan, 104));
		Assert.True(Contains(plan, 103), "a nested row is a row of its own");
		Assert.Equal("bandage", RowOf(plan, 103).ItemId);
	}

	[Fact]
	public void ThePlan_SkipsAnUnboundRowButKeepsItsBoundContents()
	{
		// The starting-supply window: a container with no id yet whose content already has one. The
		// container cannot be addressed, so it is not incarnated — its content can, so it is, and it keeps
		// the nearest id-bearing row above it as its data parent.
		var plan = PlanOf(11, Row(0, "trashbag", Row(102, "waterbottle")));

		Assert.Equal(new ulong[] { 102 }, Ids(plan));
		Assert.False(Contains(plan, 0), "an id-less row is not addressable, so the data does not ask for an object");
		Assert.Equal(0UL, ParentOf(plan, 102));
	}

	[Fact]
	public void ThePlan_LeavesAWorldRowAndItsWholeSubtreeToTheWorldPath()
	{
		// The id moved into the world: the category ends, and the world path materializes that container
		// AND restores its contents as real children. Planning them here would put two objects on one id.
		var plan = PlanOf(11, id => id == 101, [Row(101, "trashbag", Row(102, "waterbottle"))]);

		Assert.Empty(Ids(plan));
		Assert.False(Contains(plan, 101));
		Assert.False(Contains(plan, 102));
	}

	[Fact]
	public void ThePlan_KeepsTheFirstPositionOfADuplicateIdAndNeverWalksItTwice()
	{
		// The captured tree has one row per item; a repeat is a corrupt capture, and recursing into it is
		// the one answer that cannot terminate.
		var plan = PlanOf(11, Row(101, "trashbag", Row(102, "bread")), Row(101, "trashbag", Row(103, "bandage")));

		Assert.Equal(new ulong[] { 101, 102 }, Ids(plan));
	}

	[Fact]
	public void ThePlan_WantsNothingFromAnEmptyTree()
	{
		var plan = PlanOf(11);

		Assert.Empty(Ids(plan));
		Assert.Equal(0, Count(plan));
		Assert.False(Contains(plan, 0));
	}

	private static CharacterItemMsg Row(ulong instanceId, string definitionId, params CharacterItemMsg[] contents) => new()
	{
		InstanceId = instanceId,
		ItemId = definitionId,
		Condition = 1f,
		Contents = [.. contents],
	};

	private static object PlanOf(ulong owner, params CharacterItemMsg[] items) => PlanOf(owner, null, items);

	private static object PlanOf(ulong owner, Func<ulong, bool>? isWorldRow, CharacterItemMsg[] items) =>
		Build.Invoke(null, [owner, items.ToList(), isWorldRow ?? (_ => false)])!;

	private static List<ulong> Ids(object plan) => ((IEnumerable)Property(plan, "Ids")).Cast<ulong>().ToList();

	private static int Count(object plan) => (int)Property(plan, "Count");

	private static ulong Owner(object plan) => (ulong)Property(plan, "Owner");

	private static ulong ParentOf(object plan, ulong instanceId) => (ulong)ParentOfMethod.Invoke(plan, [instanceId])!;

	private static CharacterItemMsg RowOf(object plan, ulong instanceId) =>
		(CharacterItemMsg)RowMethod.Invoke(plan, [instanceId])!;

	private static bool Contains(object plan, ulong instanceId) => (bool)ContainsMethod.Invoke(plan, [instanceId])!;

	private static object Property(object plan, string name) =>
		Plan.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(plan)
		?? throw new InvalidOperationException($"StandingItemPlan.{name} not found.");

	private static MethodInfo Method(string name) =>
		Plan.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
		?? throw new InvalidOperationException($"StandingItemPlan.{name} not found.");
}
