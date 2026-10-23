using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.RecursiveCrafting.Core.Crafting;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Crafting;

/// <summary>
/// The recursive-crafting resolver: the three entry states the recipe panel shows (craftable now, reachable
/// through steps, not craftable), the steps a chain runs in order, and the two details the ticket calls out as
/// the difference between usable and broken — an ingredient's own properties (quality) and the condition the
/// player's partially spent stacks still carry ("two into one").
/// </summary>
public class CraftChainResolverTests
{
	private static CraftRecipe Recipe(string id, string result, params CraftIngredient[] ingredients) =>
		new(id, result, ingredients);

	private static CraftRecipe RecipeOf(string id, string result, int resultAmount, params CraftIngredient[] ingredients) =>
		new(id, result, ingredients, resultAmount);

	private static CraftIngredient Line(string id, int amount = 1, string? quality = null, float condition = 1f) =>
		new(id, amount, quality, condition);

	private static CraftStack Held(string id, int amount = 1, float condition = 1f, params string[] qualities) =>
		new(id, amount, condition, qualities);

	/// <summary>A tag line: no item id, a crafting quality any held unit carrying it may satisfy.</summary>
	private static CraftIngredient Tag(string quality, float condition = 1f) => CraftIngredient.OfQuality(quality, condition);

	private static CraftResolution Resolve(CraftRecipe target, IEnumerable<CraftRecipe> recipes, params CraftStack[] inventory) =>
		CraftChainResolver.Resolve(target, recipes.ToList(), inventory.ToList());

	[Fact]
	public void EveryIngredientHeld_IsCraftableNow()
	{
		var target = Recipe("bandage", "bandage", Line("rag"), Line("disinfectant"));

		var result = Resolve(target, [], Held("rag"), Held("disinfectant"));

		Assert.Equal(CraftReachability.Now, result.Reachability);
		Assert.Empty(result.Steps);
		Assert.Null(result.Blocker);
	}

	[Fact]
	public void AMissingIngredientWithARecipe_IsReachableThroughThatStep()
	{
		var target = Recipe("bandage", "bandage", Line("rag"));
		var ripped = Recipe("rip-rag", "rag", Line("cloth"));

		var result = Resolve(target, [ripped], Held("cloth"));

		Assert.Equal(CraftReachability.ThroughSteps, result.Reachability);
		Assert.Equal([new CraftStep("rip-rag", 1)], result.Steps);
	}

	[Fact]
	public void ANestedChain_ListsTheStepsDeepestFirst()
	{
		var target = Recipe("bandage", "bandage", Line("rag"));
		var ripped = Recipe("rip-rag", "rag", Line("cloth"));
		var weave = Recipe("weave-cloth", "cloth", Line("fiber"));

		var result = Resolve(target, [ripped, weave], Held("fiber"));

		Assert.Equal(CraftReachability.ThroughSteps, result.Reachability);
		Assert.Equal([new CraftStep("weave-cloth", 1), new CraftStep("rip-rag", 1)], result.Steps);
	}

	[Fact]
	public void AnIngredientNothingProduces_IsUnreachableAndNamesTheBlocker()
	{
		var target = Recipe("bandage", "bandage", Line("rag"));

		var result = Resolve(target, [], Held("cloth"));

		Assert.Equal(CraftReachability.Unreachable, result.Reachability);
		Assert.Equal("rag", result.Blocker);
	}

	/// <summary>The "two into one" case: two half-spent items of the same id satisfy a line asking for one unit at full condition.</summary>
	[Fact]
	public void TwoHalfSpentStacks_SatisfyOneFreshLine()
	{
		var target = Recipe("bandage", "bandage", Line("rag", amount: 1, condition: 1f));

		var result = Resolve(target, [], Held("rag", amount: 1, condition: 0.5f), Held("rag", amount: 1, condition: 0.5f));

		Assert.Equal(CraftReachability.Now, result.Reachability);
	}

	/// <summary>Half a condition short is short: the resolver does not round a spent stack up.</summary>
	[Fact]
	public void SpentStacksShortOfTheCondition_DoNotSatisfyTheLine()
	{
		var target = Recipe("bandage", "bandage", Line("rag"));

		var result = Resolve(target, [], Held("rag", amount: 1, condition: 0.5f));

		Assert.Equal(CraftReachability.Unreachable, result.Reachability);
	}

	/// <summary>A line naming a quality takes only units carrying it; a crafted unit carries none in this model.</summary>
	[Fact]
	public void AQualityLine_TakesOnlyMatchingUnits()
	{
		var target = Recipe("bandage", "bandage", Line("herb", quality: "strong"));

		Assert.Equal(CraftReachability.Now, Resolve(target, [], Held("herb", qualities: ["strong"])).Reachability);
		Assert.Equal(CraftReachability.Unreachable, Resolve(target, [], Held("herb", qualities: ["weak"])).Reachability);
	}

	/// <summary>
	/// A TAG line names no item: any unit carrying the quality satisfies it, and a crafted unit (no qualities in
	/// this model) cannot — so the tag is reported as the blocker instead of being invented.
	/// </summary>
	[Fact]
	public void ATagLine_IsSatisfiedByAnyUnitCarryingTheQuality()
	{
		var target = Recipe("rope", "rope", Tag("foliage", condition: 2f));

		Assert.Equal(CraftReachability.Now, Resolve(target, [], Held("leaf", condition: 1f, qualities: ["foliage", "soft"]), Held("vine", condition: 1f, qualities: ["foliage"])).Reachability);
		Assert.Equal(CraftReachability.Unreachable, Resolve(target, [], Held("stone", qualities: ["hard"])).Reachability);
	}

	/// <summary>A tag line is not produced by a recipe in this model: the resolver says so rather than guessing what a crafted unit would carry.</summary>
	[Fact]
	public void ATagLineWithNoHeldUnit_NamesTheTag()
	{
		var target = Recipe("rope", "rope", Tag("foliage"));
		var recipe = Recipe("weave-vine", "vine", Line("fiber"));

		var result = Resolve(target, [recipe], Held("fiber"));

		Assert.Equal(CraftReachability.Unreachable, result.Reachability);
		Assert.Equal("foliage", result.Blocker);
	}

	[Fact]
	public void ACycle_IsNotAChain_AndResolvesAsUnreachable()
	{
		var target = Recipe("bandage", "bandage", Line("a"));
		var a = Recipe("make-a", "a", Line("b"));
		var b = Recipe("make-b", "b", Line("a"));

		var result = Resolve(target, [a, b], Held("nothing"));

		Assert.Equal(CraftReachability.Unreachable, result.Reachability);
		Assert.Equal("a", result.Blocker);
	}

	[Fact]
	public void AStepProducingTwoUnits_SatisfiesATwoUnitLineInOneRun()
	{
		var target = Recipe("bandage", "bandage", Line("rag", amount: 2));
		var pair = RecipeOf("cut-two", "rag", resultAmount: 2, Line("cloth", amount: 2));

		var result = Resolve(target, [pair], Held("cloth", amount: 2));

		Assert.Equal(CraftReachability.ThroughSteps, result.Reachability);
		Assert.Equal([new CraftStep("cut-two", 1)], result.Steps);
	}

	[Fact]
	public void TwoRecipesForTheSameItem_PickTheLowerIdDeterministically()
	{
		var target = Recipe("bandage", "bandage", Line("rag"));
		var z = Recipe("z-rip", "rag", Line("cloth"));
		var a = Recipe("a-rip", "rag", Line("cloth"));

		var result = Resolve(target, [z, a], Held("cloth"));

		Assert.Equal([new CraftStep("a-rip", 1)], result.Steps);
	}

	[Fact]
	public void AChainDeeperThanTheLimit_IsUnreachable()
	{
		var target = Recipe("bandage", "bandage", Line("item-0"));
		var recipes = new List<CraftRecipe>();
		for (var i = 0; i < CraftChainResolver.MaximumDepth + 2; i++)
		{
			recipes.Add(Recipe($"link-{i}", $"item-{i}", Line($"item-{i + 1}")));
		}

		var result = Resolve(target, recipes, Held($"item-{CraftChainResolver.MaximumDepth + 2}"));

		Assert.Equal(CraftReachability.Unreachable, result.Reachability);
	}

	/// <summary>The three states read as three different sentences — the panel's third state has to be distinguishable from both others.</summary>
	[Fact]
	public void TheReport_WordsTheThreeStatesDifferently()
	{
		var now = Recipe("bandage", "bandage", Line("rag"));
		var ripped = Recipe("rip-rag", "rag", Line("cloth"));

		var nowText = CraftChainReport.Describe("bandage", Resolve(now, [], Held("rag")));
		var stepsText = CraftChainReport.Describe("bandage", Resolve(now, [ripped], Held("cloth")));
		var blockedText = CraftChainReport.Describe("bandage", Resolve(now, [], Held("stone")));

		Assert.Equal("bandage: craftable now.", nowText);
		Assert.Equal("bandage: reachable through 1 step(s) — 1. rip-rag.", stepsText);
		Assert.Equal("bandage: cannot be crafted — nothing here provides \"rag\".", blockedText);
	}

	/// <summary>A step run more than once reads its count, not a repeated entry.</summary>
	[Fact]
	public void TheReport_ShowsHowOftenAStepRuns()
	{
		var target = Recipe("bandage", "bandage", Line("rag", amount: 2));
		var single = Recipe("rip-one", "rag", Line("cloth"));

		var text = CraftChainReport.Describe("bandage", Resolve(target, [single], Held("cloth", amount: 2)));

		Assert.Equal("bandage: reachable through 2 step(s) — 1. rip-one, 2. rip-one.", text);
	}
}
