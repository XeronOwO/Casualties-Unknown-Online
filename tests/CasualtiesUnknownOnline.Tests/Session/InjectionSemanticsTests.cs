using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using CasualtiesUnknownOnline.Tests.Fakes;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The game-data half of the cross-player injection chain
/// (<c>mod-cross-player-native-semantics</c>, Part A): the drain arithmetic the
/// host still owns — the game's own <c>WaterContainerItem.CalculateDrain</c>
/// shape, with no per-content constant — and the admission verdict the native
/// dispatch itself makes (<c>usableOnLimb</c> container + <c>injectable</c>
/// liquid). The curated per-item/per-ml catalog this replaces was deleted with
/// the catalog; the effect itself now belongs to the patient's own client, so
/// nothing here pins a coefficient.
/// </summary>
public class InjectionSemanticsTests
{
	[Fact]
	public void DrainPlan_SplitsTheRequestedAmountInProportion()
	{
		// A mixed container is the native case: the draw is proportional across
		// every stack, and the inert half simply carries no effect.
		var plan = Drain(
			[("morphine", 10f), ("water", 90f)],
			50f);

		Assert.Equal(2, plan.Count);
		Assert.Equal("morphine", plan[0].LiquidId);
		Assert.Equal(5f, plan[0].Amount, 3);
		Assert.Equal("water", plan[1].LiquidId);
		Assert.Equal(45f, plan[1].Amount, 3);
	}

	[Fact]
	public void DrainPlan_CapsAtTheContainerTotal()
	{
		var plan = Drain([("morphine", 30f)], 100f);

		Assert.Single(plan);
		Assert.Equal(30f, plan[0].Amount, 3);
	}

	[Fact]
	public void DrainPlan_RefusesAnEmptyContainerOrANonPositiveDraw()
	{
		Assert.False(LiquidDrainPlan.TryCreate([], 50f, out var empty));
		Assert.Empty(empty);
		Assert.False(LiquidDrainPlan.TryCreate(Stacks([("morphine", 10f)]), 0f, out _));
		Assert.False(LiquidDrainPlan.TryCreate(Stacks([("morphine", 0f)]), 10f, out _));
	}

	[Fact]
	public void Admission_AcceptsALimbUsableContainerWithAnInjectableStack()
	{
		Assert.True(InjectionAdmission.IsInjectableContainer(
			FakeLimbUseSemantics.Instance,
			"morphine",
			Stacks([("morphine", 100f)])));
	}

	[Fact]
	public void Admission_AcceptsAMixedContainerTheOldCatalogRefused()
	{
		// `water` is the solvent the deleted catalog needed an inert allowlist for;
		// under the native path it is drained and applies nothing, which is the
		// game's own behaviour rather than an approximation to refuse.
		Assert.True(InjectionAdmission.IsInjectableContainer(
			FakeLimbUseSemantics.Instance,
			"fentanyl",
			Stacks([("water", 90f), ("fentanyl", 10f)])));
	}

	[Fact]
	public void Admission_RefusesALimbUsableContainerWithNoInjectableLiquid()
	{
		// A syringe full of water: the item may be drawn from, but nothing in it
		// is injectable, so this is not the injection chain's business.
		Assert.False(InjectionAdmission.IsInjectableContainer(
			FakeLimbUseSemantics.Instance,
			"syringe",
			Stacks([("water", 100f)])));
	}

	[Fact]
	public void Admission_RefusesAnItemTheGameDoesNotDrawFromALimb()
	{
		// A water bottle is a liquid container, but not one a limb action draws
		// from: the game's own data refuses it, and so does the chain.
		Assert.False(InjectionAdmission.IsInjectableContainer(
			FakeLimbUseSemantics.Instance,
			"waterbottle",
			Stacks([("morphine", 100f)])));
	}

	[Fact]
	public void StartValidator_RefusesAnItemOutsideTheChainsOwnData()
	{
		var accepted = InjectionStartValidator.TryValidateOperator(
			FakeLimbUseSemantics.Instance,
			Operator(Container(42, "waterbottle", ("morphine", 100f))),
			42,
			out var itemIndex,
			out var reason);

		Assert.False(accepted);
		// The index still names the item the validator rejected, so the caller's
		// log points at the item the operator actually offered.
		Assert.Equal(0, itemIndex);
		Assert.Equal("Item is not injectable.", reason);
	}

	[Fact]
	public void StartValidator_AcceptsAnInjectableContainerAndNamesItsIndex()
	{
		var accepted = InjectionStartValidator.TryValidateOperator(
			FakeLimbUseSemantics.Instance,
			Operator(Container(7, "saline", ("saline", 750f)), Container(42, "combatpen", ("highgradestimulant", 60f))),
			42,
			out var itemIndex,
			out var reason);

		Assert.True(accepted);
		Assert.Equal(1, itemIndex);
		Assert.Equal("", reason);
	}

	[Fact]
	public void LimbRule_TheRequestedLimbWinsAndAnInvalidOneFallsBackToTheMostInjured()
	{
		// The rule the TARGET's own client applies to a dose whose limb the operator
		// did not name (a -1 limbIndex is legal for an injection —
		// MedicalTargetBodyValidator does not scope it): NativeInjectionApply.ResolveLimb
		// mirrors this function on a live Body, which an L0 host cannot reach. Pinning
		// the rule here keeps its boundaries (the operator's pick wins; a dismembered or
		// absent one falls back) a test rather than a comment, which is what the deleted
		// catalog's three limb-choice cases used to cover.
		IReadOnlyList<CharacterLimbMsg> limbs =
		[
			new CharacterLimbMsg { Index = 0, SkinHealth = 50f, MuscleHealth = 50f },
			new CharacterLimbMsg { Index = 1, SkinHealth = 20f, MuscleHealth = 30f },
			new CharacterLimbMsg { Index = 2, SkinHealth = 80f, MuscleHealth = 80f, Dismembered = true },
		];

		Assert.Equal(0, RemoteHealApplication.ResolveLimbIndex(limbs, 0));   // the operator's own pick wins
		Assert.Equal(1, RemoteHealApplication.ResolveLimbIndex(limbs, -1));  // auto: the most injured attachable limb
		Assert.Equal(1, RemoteHealApplication.ResolveLimbIndex(limbs, 2));   // a dismembered pick falls back
		Assert.Equal(1, RemoteHealApplication.ResolveLimbIndex(limbs, 9));   // an absent pick falls back
	}

	private static List<LiquidStackMsg> Drain((string LiquidId, float Amount)[] liquids, float amount)
	{
		Assert.True(LiquidDrainPlan.TryCreate(Stacks(liquids), amount, out var plan));
		return plan;
	}

	private static List<LiquidStackMsg> Stacks((string LiquidId, float Amount)[] liquids) =>
		[.. liquids.Select(l => new LiquidStackMsg { LiquidId = l.LiquidId, Amount = l.Amount })];

	private static CharacterDataMsg Operator(params CharacterItemMsg[] items) => new()
	{
		OwnerSteamId = 77,
		Items = [.. items],
	};

	private static CharacterItemMsg Container(ulong instanceId, string itemId, params (string LiquidId, float Amount)[] liquids) => new()
	{
		InstanceId = instanceId,
		ItemId = itemId,
		SlotIndex = 0,
		Condition = 1f,
		Liquids = Stacks(liquids),
	};
}
