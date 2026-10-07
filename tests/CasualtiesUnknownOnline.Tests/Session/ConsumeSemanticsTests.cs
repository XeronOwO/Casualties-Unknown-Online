using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using CasualtiesUnknownOnline.Tests.Fakes;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The game-data half of the cross-player drink chain
/// (<c>mod-cross-player-native-semantics</c>, Part B): the admission verdict the
/// game's own dispatch makes (<c>Body.UseItem</c>'s <c>LiquidItemInfo.usable</c>
/// gate) and the drain arithmetic the host still owns — the game's own
/// <c>WaterContainerItem.CalculateDrain</c> shape, fed by the ml the item's own
/// use action computed on the operator's client.
/// <para>
/// The curated per-item ml table, the per-liquid effect coefficient table and the
/// host's mindwipe mirror were deleted with the catalogs; the effect itself now
/// belongs to the patient's own client (each liquid's <c>onDrink</c>), so nothing
/// here pins a coefficient. What the deleted liquid allowlist used to decide is
/// pinned by the admission cases: the LIQUID decides nothing — only the item's own
/// flag and whether it still holds liquid.
/// </para>
/// </summary>
public class ConsumeSemanticsTests
{
	[Fact]
	public void Admission_AcceptsADrinkContainerHoldingLiquid()
	{
		Assert.True(ConsumeAdmission.IsDrinkContainer(
			FakeConsumeSemantics.Instance,
			"waterbottle",
			Stacks([("water", 500f)])));
	}

	[Fact]
	public void Admission_AcceptsALiquidTheDeletedAllowlistNeverCarried()
	{
		// The deleted catalog's 14-liquid list refused a container as a whole
		// unless EVERY stack was on it. Native Drink applies no liquid gate at all:
		// it drains what the container holds and runs whichever liquids the
		// registry knows, so a liquid CUO never listed is the game's own case
		// rather than a reason to refuse the gesture.
		Assert.True(ConsumeAdmission.IsDrinkContainer(
			FakeConsumeSemantics.Instance,
			"waterbottle",
			Stacks([("mystery", 90f), ("water", 10f)])));
	}

	[Fact]
	public void Admission_RefusesAnItemTheGameOnlyDrawsFromALimb()
	{
		// A syringe holds water and can be drawn from a limb, but the game cannot
		// USE it: `usable` is false, which is the flag Body.UseItem gates the item's
		// own use action on. The deleted generic tail asked only the liquid, so this
		// item used to be fed to a teammate.
		Assert.False(ConsumeAdmission.IsDrinkContainer(
			FakeConsumeSemantics.Instance,
			"syringe",
			Stacks([("water", 100f)])));
	}

	[Fact]
	public void Admission_RefusesAnEmptyContainer()
	{
		Assert.False(ConsumeAdmission.IsDrinkContainer(FakeConsumeSemantics.Instance, "waterbottle", []));
		Assert.False(ConsumeAdmission.IsDrinkContainer(FakeConsumeSemantics.Instance, "waterbottle", null));
	}

	[Fact]
	public void TheDrinkRuleAndTheLimbRuleBothAnswerForTheContainersTheGameMarksBothWays()
	{
		// What this pins: the two rules are NOT disjoint — a container the game
		// marks usable AND usableOnLimb (saline, ringersolution, the blood bags) is
		// admitted by both — so what keeps such an item on one chain is the ORDER
		// the routing sites ask them in, injection first. That order is pinned
		// where it is observable:
		// ItemUseTests.Use_ADrinkableInjectableContainer_IsRefusedByTheInjectionFirstOrder.
		// What it does NOT pin: the game's own content. A readable gate cannot read
		// a game assembly, so the carriers are the census in
		// ItemAndBodySoundCaptureGateTests (`VanillaInjectableCarriers`,
		// `VanillaTopicalCarriers`) and this suite's own fake lists.
		var saline = Stacks([("saline", 100f)]);
		Assert.True(ConsumeAdmission.IsDrinkContainer(FakeConsumeSemantics.Instance, "saline", saline));
		Assert.True(InjectionAdmission.IsInjectableContainer(FakeLimbUseSemantics.Instance, "saline", saline));

		// A drink container the limb rules cannot claim: the flag is the item's own.
		var water = Stacks([("water", 500f)]);
		Assert.True(ConsumeAdmission.IsDrinkContainer(FakeConsumeSemantics.Instance, "waterbottle", water));
		Assert.False(InjectionAdmission.IsInjectableContainer(FakeLimbUseSemantics.Instance, "waterbottle", water));
		Assert.False(TopicalAdmission.IsTopicalContainer(FakeLimbUseSemantics.Instance, "waterbottle", water));
	}

	[Fact]
	public void TheHostCapsTheDoseAtWhatTheItemReallyCarries()
	{
		// The operator measures the item's own delegate amount (100 ml for a water
		// bottle), and the authoritative item may hold less: the host caps it the way
		// native CalculateDrain does instead of trusting the reported number.
		Assert.True(LiquidDrainPlan.TryCreate(Stacks([("water", 40f)]), 100f, out var plan));
		var drain = Assert.Single(plan);
		Assert.Equal("water", drain.LiquidId);
		Assert.Equal(40f, drain.Amount, 3);
	}

	[Fact]
	public void TheHostRefusesADrinkWhoseGestureMeasuredNothing()
	{
		// A request that carries no dose cannot be turned into a draw, which is the
		// native path's own early return for a container with nothing to give.
		Assert.False(LiquidDrainPlan.TryCreate(Stacks([("water", 500f)]), 0f, out var plan));
		Assert.Empty(plan);
	}

	[Fact]
	public void TheHostRefusesADoseThatIsNotAPositiveFiniteNumber()
	{
		// The amount is a client-reported dose, so it is asked to be a real number
		// and not merely positive: NaN passes a bare `amount <= 0f` test and then
		// propagates through Math.Min into every stack's amount, poisoning the
		// item's liquid state, and Infinity would drain the whole container.
		Assert.False(LiquidDrainPlan.TryCreate(Stacks([("water", 500f)]), float.NaN, out var nanPlan));
		Assert.Empty(nanPlan);
		Assert.False(LiquidDrainPlan.TryCreate(Stacks([("water", 500f)]), float.PositiveInfinity, out var infinitePlan));
		Assert.Empty(infinitePlan);
	}

	private static List<LiquidStackMsg> Stacks((string LiquidId, float Amount)[] liquids) =>
		[.. liquids.Select(l => new LiquidStackMsg { LiquidId = l.LiquidId, Amount = l.Amount })];
}
