using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using CasualtiesUnknownOnline.Tests.Fakes;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The game-data half of the cross-player topical chain
/// (<c>mod-cross-player-native-semantics</c>, Part B): the admission verdict the
/// native dispatch itself makes (<c>usableOnLimb</c> container + <c>healthUsable</c>
/// liquid) and the drain arithmetic the host still owns — the game's own
/// <c>WaterContainerItem.CalculateDrain</c> shape, fed by the dose the item's own
/// delegate computed on the operator's client.
/// <para>
/// The curated per-item ml table and the per-ml effect table this replaces were
/// deleted with the catalog; the effect itself now belongs to the patient's own
/// client, so nothing here pins a coefficient. Neither chain resolves a limb on the
/// host: a topical request must name the one the WOUND VIEW carried (decision 246),
/// and the injection chain's automatic rule — the shared one the deleted catalog's
/// cases also covered
/// (<c>InjectionSemanticsTests.LimbRule_TheRequestedLimbWinsAndAnInvalidOneFallsBackToTheMostInjured</c>)
/// — is the injection chain's alone.
/// </para>
/// </summary>
public class TopicalSemanticsTests
{
	[Fact]
	public void Admission_AcceptsALimbUsableContainerWithAHealthUsableStack()
	{
		Assert.True(TopicalAdmission.IsTopicalContainer(
			FakeLimbUseSemantics.Instance,
			"paincream",
			Stacks([("reliefcream", 100f)])));
	}

	[Fact]
	public void Admission_AcceptsAMixedContainerTheOldCatalogRefused()
	{
		// The deleted catalog refused a container as a whole unless EVERY stack was
		// one of its six liquids. Native ApplyToLimb draws every stack and applies
		// an effect only where `healthUsable` is set, so a solvent is the game's own
		// case rather than a reason to refuse the gesture.
		Assert.True(TopicalAdmission.IsTopicalContainer(
			FakeLimbUseSemantics.Instance,
			"spraybottle",
			Stacks([("water", 90f), ("disinfectant", 10f)])));
	}

	[Fact]
	public void Admission_RefusesALimbUsableContainerWithNoHealthUsableLiquid()
	{
		// A syringe full of water: the item may be drawn from, but nothing in it is
		// health-usable, so this is not the topical chain's business.
		Assert.False(TopicalAdmission.IsTopicalContainer(
			FakeLimbUseSemantics.Instance,
			"syringe",
			Stacks([("water", 100f)])));
	}

	[Fact]
	public void Admission_RefusesAnItemTheGameDoesNotDrawFromALimb()
	{
		Assert.False(TopicalAdmission.IsTopicalContainer(
			FakeLimbUseSemantics.Instance,
			"waterbottle",
			Stacks([("reliefcream", 100f)])));
	}

	[Fact]
	public void Admission_RefusesAnEmptyContainer()
	{
		Assert.False(TopicalAdmission.IsTopicalContainer(FakeLimbUseSemantics.Instance, "paincream", []));
		Assert.False(TopicalAdmission.IsTopicalContainer(FakeLimbUseSemantics.Instance, "paincream", null));
	}

	[Fact]
	public void TheTwoRulesAreDisjointOnTheContentThisSuiteModels()
	{
		// What this pins: each rule answers for its own family and refuses the
		// other's, on the content the suite models. What it does NOT pin: the game's
		// own content. A readable gate cannot read a game assembly, so the vanilla
		// carriers are a hand-written census in
		// ItemAndBodySoundCaptureGateTests (`VanillaInjectableCarriers`,
		// `VanillaTopicalCarriers`) — and the game's liquids really are disjoint
		// today (six `healthUsable`, twenty-six `injectable`, no overlap in
		// Liquids.cs). The routing ORDER at the three production sites is pinned
		// where it is observable: ItemUseTests'
		// Use_AContainerHoldingBothKinds_IsRefusedByTheInjectionFirstOrder.
		var injectable = Stacks([("fentanyl", 100f)]);
		Assert.False(TopicalAdmission.IsTopicalContainer(FakeLimbUseSemantics.Instance, "fentanyl", injectable));
		Assert.True(InjectionAdmission.IsInjectableContainer(FakeLimbUseSemantics.Instance, "fentanyl", injectable));

		var healthUsable = Stacks([("reliefcream", 100f)]);
		Assert.True(TopicalAdmission.IsTopicalContainer(FakeLimbUseSemantics.Instance, "paincream", healthUsable));
		Assert.False(InjectionAdmission.IsInjectableContainer(FakeLimbUseSemantics.Instance, "paincream", healthUsable));
	}

	[Fact]
	public void TheHostDrawsTheDoseTheItemsOwnDelegateComputed()
	{
		// paincream's delegate is `ApplyToLimb(limb, 10f)` (Item.cs:650-653), so the
		// dose the operator measured is 10 ml and the host's draw is exactly that —
		// no per-item constant survives on this side.
		Assert.True(LiquidDrainPlan.TryCreate(Stacks([("reliefcream", 100f)]), 10f, out var plan));
		var drain = Assert.Single(plan);
		Assert.Equal("reliefcream", drain.LiquidId);
		Assert.Equal(10f, drain.Amount, 3);
	}

	[Fact]
	public void TheHostCapsTheDoseAtWhatTheItemReallyCarries()
	{
		// The operator measures the delegate's own amount, and the authoritative
		// item may hold less than that: the host caps it the way native
		// CalculateDrain does instead of trusting the reported number.
		Assert.True(LiquidDrainPlan.TryCreate(Stacks([("reliefcream", 4f)]), 10f, out var plan));
		var drain = Assert.Single(plan);
		Assert.Equal(4f, drain.Amount, 3);
	}

	[Fact]
	public void TheHostRefusesAUseWhoseGestureMeasuredNothing()
	{
		// A request that carries no dose cannot be turned into a draw, which is the
		// native path's own early return for a container with nothing to give.
		Assert.False(LiquidDrainPlan.TryCreate(Stacks([("reliefcream", 100f)]), 0f, out var plan));
		Assert.Empty(plan);
	}

	private static List<LiquidStackMsg> Stacks((string LiquidId, float Amount)[] liquids) =>
		[.. liquids.Select(l => new LiquidStackMsg { LiquidId = l.LiquidId, Amount = l.Amount })];
}
