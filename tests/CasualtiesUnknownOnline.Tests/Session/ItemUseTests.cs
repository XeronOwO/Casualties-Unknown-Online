using System;
using System.Linq;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static CasualtiesUnknownOnline.Tests.Session.PlayerInteractionTestSession;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Behavior family: using a consumable item on another player (drink/food/medicine/topical application and refusals).
/// Split from PlayerInteractionServiceTests so xUnit v2 (one collection per class,
/// serial inside a class) does not serialize the whole player-interaction surface.
/// </summary>
[Trait("Category", "Integration")]
public class ItemUseTests
{
	[Fact]
	public void Guest_UsesAnalgesicGauzeOnHost_AddsOpiateComponentAndSendsResult()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: false));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, Item(42, "analgesicgauze", slot: 0)));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendHealRequest(HostId);

		var result = HealResult(received);
		Assert.Equal(GuestId, result.HealerSteamId);
		Assert.Equal(HostId, result.TargetSteamId);
		Assert.True(result.ItemDestroyed);
		Assert.Equal(1, result.HealedLimbIndex);
		Assert.Equal(28f, result.Health!.OpiateAmount);

		var hostData = characters.GetHostCharacterData()!;
		Assert.Equal(28f, hostData.Health!.OpiateAmount);
		Assert.True(hostData.Limbs[result.HealedLimbIndex].SkinHealAmount > 0f);
		Assert.DoesNotContain(characters.GetSavedCharacter(GuestId)!.Items, i => i.InstanceId == 42);
	}

	[Fact]
	public void Guest_UsesWaterOnHost_CommitsTheDoseAndCarriesItToThePatient()
	{
		// Part B of mod-cross-player-native-semantics, the drink chain: the
		// operator's own client measured 100 ml from the water bottle's own use
		// action (`Drink(body, 100f, "drink")`, Item.cs:3171) and the request
		// carries it. The host commits the drain and carries it as DrinkDose; the
		// EFFECT belongs to the patient's client (the water liquid's own onDrink),
		// so the host computes no body state and writes none.
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var items = host.Services.GetRequiredService<IItemControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: true));
		var water = WaterBottle(42);
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, water));
		items.AdoptTransferredItem(GuestId, 42, water);

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendUseRequest(HostId, 42, doseMl: 100f);

		var result = UseResult(received);
		Assert.Equal(GuestId, result.UserSteamId);
		Assert.Equal(HostId, result.TargetSteamId);
		Assert.Equal(42UL, result.ItemInstanceId);
		Assert.False(result.ItemDestroyed);
		Assert.NotNull(result.ItemAfter);
		Assert.True(Math.Abs(result.ItemAfter!.Condition - 0.8f) < 0.001f);

		var dose = Assert.Single(result.DrinkDose);
		Assert.Equal("water", dose.LiquidId);
		Assert.True(Math.Abs(dose.Amount - 100f) < 0.001f);

		// No host-computed body state for this family, and the topical family's own
		// dose member stays empty: the two name different native calls on the
		// affected side.
		Assert.Null(result.Health);
		Assert.Empty(result.Limbs);
		Assert.Empty(result.AppliedDose);

		var hostData = characters.GetHostCharacterData()!;
		Assert.Equal(0f, hostData.Health!.Thirst);

		var saved = characters.GetSavedCharacter(GuestId)!.Items.Single(i => i.InstanceId == 42);
		Assert.True(Math.Abs(saved.Condition - 0.8f) < 0.001f);
		Assert.True(Math.Abs(saved.Liquids.Single(l => l.LiquidId == "water").Amount - 400f) < 0.001f);
		var transferred = items.GetTransferredItems(GuestId).Single(w => w.Item.InstanceId == 42);
		Assert.True(Math.Abs(transferred.Item.Condition - 0.8f) < 0.001f);
		Assert.True(Math.Abs(transferred.Item.Liquids.Single(l => l.LiquidId == "water").Amount - 400f) < 0.001f);
	}

	[Fact]
	public void Guest_UseResult_ProjectsUseEventOnBothParticipants()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var items = host.Services.GetRequiredService<IItemControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: true));
		var water = WaterBottle(42);
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, water));
		items.AdoptTransferredItem(GuestId, 42, water);

		PlayerItemUseResultMsg? hostUse = null;
		PlayerItemUseResultMsg? guestUse = null;
		host.Services.GetRequiredService<IPlayerInteractionControl>().UseReceived += m => hostUse = m;
		guest.Services.GetRequiredService<IPlayerInteractionControl>().UseReceived += m => guestUse = m;

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendUseRequest(HostId, 42, doseMl: 100f);

		Assert.NotNull(hostUse);
		Assert.Equal(GuestId, hostUse!.UserSteamId);
		Assert.Equal(HostId, hostUse.TargetSteamId);
		Assert.NotNull(guestUse);
		Assert.Equal(GuestId, guestUse!.UserSteamId);
		Assert.Equal(HostId, guestUse.TargetSteamId);
	}

	[Fact]
	public void Host_OffersBreadToGuest_AsksTheEaterToRunTheGamesOwnEat()
	{
		// The solid-food family's request half (mod-cross-player-solid-food-semantics,
		// step 3): the host computes NOTHING. A food's use action writes the eating
		// body AND the item it is handed, so the eat belongs to the affected side's
		// client, which owns both — and the host's whole part of the request is the
		// admission that authorizes the one outcome report it will receive.
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true, Item(77, "bread", slot: 0)));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		host.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendUseRequest(GuestId, 77);

		var result = UseResult(received);
		Assert.Equal(HostId, result.UserSteamId);
		Assert.Equal(GuestId, result.TargetSteamId);
		Assert.Equal(77UL, result.ItemInstanceId);
		Assert.True(result.TargetEatsTheItem);

		// No host-computed body state and no item state: the eat has not run yet, and
		// the item's post-eat state arrives with the eater's own report.
		Assert.Null(result.Health);
		Assert.Empty(result.Limbs);
		Assert.Null(result.ItemAfter);
		Assert.False(result.ItemDestroyed);
		Assert.Null(result.WornItem);
		Assert.Empty(result.AppliedDose);
		Assert.Empty(result.DrinkDose);

		// Nothing was consumed, destroyed or spawned anywhere: the owner's snapshot
		// still carries the whole loaf and the kernel holds no item fact for it (the
		// family that consumed it would have spawned/updated one).
		Assert.True(Math.Abs(characters.GetHostCharacterData()!.Items.Single(i => i.InstanceId == 77).Condition - 0.75f) < 0.001f);
		Assert.Null(host.Services.GetRequiredService<ItemKernelAuthority>().FindItem(77));
	}

	[Fact]
	public void Guest_EatsTheBreadAndReportsTheOutcome_TheHostHandsItToItsOwner()
	{
		// The outcome half: the eater's client ran the game's own eat against its own
		// body and its own object of the offered item, and reports what the item
		// became. The host commits that onto the item's OWNER — whose ownership never
		// moved — and publishes it as the ordinary use result, so the owner's own item
		// and every peer's clone learn it through the path every other family uses.
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true, Item(77, "bread", slot: 0)));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		host.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendUseRequest(GuestId, 77);
		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendItemActionOutcome(77, 0.41f, consumed: false);

		var results = UseResults(received);
		Assert.Equal(2, results.Count);
		var applied = results[1];
		Assert.Equal(HostId, applied.UserSteamId);
		Assert.Equal(GuestId, applied.TargetSteamId);
		Assert.False(applied.TargetEatsTheItem);
		Assert.False(applied.ItemDestroyed);
		Assert.NotNull(applied.ItemAfter);
		Assert.True(Math.Abs(applied.ItemAfter!.Condition - 0.41f) < 0.001f);
		Assert.Null(applied.Health);

		Assert.True(Math.Abs(characters.GetHostCharacterData()!.Items.Single(i => i.InstanceId == 77).Condition - 0.41f) < 0.001f);
		var kernelItem = host.Services.GetRequiredService<ItemKernelAuthority>().FindItem(77);
		Assert.NotNull(kernelItem);
		Assert.Equal(ItemLocationKind.Carried, kernelItem!.Value.Location.Kind);
		Assert.Equal(HostId, kernelItem.Value.Location.Owner.Value);
		Assert.True(Math.Abs(kernelItem.Value.Data.Condition - 0.41f) < 0.001f);
	}

	[Fact]
	public void Guest_EatsAFoodWhoseUseConsumesIt_AndItsOwnerLosesTheItem()
	{
		// exposedcore's own use action destroys the item object, so the item is gone
		// whatever its condition says. The shape is read from the item's own data
		// before the eat, because the eater's copy is already destroyed by the time a
		// report could carry that fact.
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true, Item(77, "exposedcore", slot: 0)));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		host.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendUseRequest(GuestId, 77);
		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendItemActionOutcome(77, 0.75f, consumed: false);

		var applied = UseResults(received)[1];
		Assert.True(applied.ItemDestroyed);
		Assert.Null(applied.ItemAfter);
		Assert.DoesNotContain(characters.GetHostCharacterData()!.Items, i => i.InstanceId == 77);
	}

	[Fact]
	public void EatOutcome_WithoutAnAdmittedEat_ChangesNothing()
	{
		// The one write the item domain refuses by rule is a member changing another
		// member's carried item, so the admission IS the authorization: an outcome for
		// an eat this host never admitted finds no grant and is refused by name.
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true, Item(77, "bread", slot: 0)));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendItemActionOutcome(77, 0.41f, consumed: false);

		Assert.Empty(UseResults(received));
		Assert.True(Math.Abs(characters.GetHostCharacterData()!.Items.Single(i => i.InstanceId == 77).Condition - 0.75f) < 0.001f);
		Assert.Null(host.Services.GetRequiredService<ItemKernelAuthority>().FindItem(77));
	}

	[Fact]
	public void Guest_EatsTheBread_TwiceReportsOnce()
	{
		// One admission, one report: a second outcome for the same eat has no grant
		// left, so a late or repeated report can never roll an item's settled state
		// back.
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true, Item(77, "bread", slot: 0)));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		host.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendUseRequest(GuestId, 77);
		var eater = guest.Services.GetRequiredService<IPlayerInteractionControl>();
		eater.SendItemActionOutcome(77, 0.41f, consumed: false);
		eater.SendItemActionOutcome(77, 0.1f, consumed: false);

		Assert.Equal(2, UseResults(received).Count);
		Assert.True(Math.Abs(characters.GetHostCharacterData()!.Items.Single(i => i.InstanceId == 77).Condition - 0.41f) < 0.001f);
	}

	[Fact]
	public void Host_EatsAsTheAffectedSide_AndItsOutcomeIsHandledFromInsideTheResultProjection()
	{
		// The host can be the affected side just as a guest can, and there its own
		// client is the one running the game's own eat: the adapter's result handler
		// (which the kernel projection calls inline) runs the action, the action's use
		// report comes straight back into this service, and the commit + second result
		// are published from inside the projection of the FIRST result. This case
		// reproduces exactly that nesting at the message level — which is the part a
		// test host can drive — with the two participants' roles swapped relative to
		// the case above.
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var items = host.Services.GetRequiredService<IItemControl>();
		var bread = Item(77, "bread", slot: 0);
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, bread));
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		items.AdoptTransferredItem(GuestId, 77, bread);

		var interaction = host.Services.GetRequiredService<IPlayerInteractionControl>();
		interaction.UseReceived += msg =>
		{
			if (msg.TargetEatsTheItem)
			{
				interaction.SendItemActionOutcome(77, 0.41f, consumed: false);
			}
		};

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendUseRequest(HostId, 77);

		var results = UseResults(received);
		Assert.Equal(2, results.Count);
		Assert.True(results[0].TargetEatsTheItem);
		Assert.Equal(GuestId, results[1].UserSteamId);
		Assert.Equal(HostId, results[1].TargetSteamId);
		Assert.True(Math.Abs(results[1].ItemAfter!.Condition - 0.41f) < 0.001f);

		Assert.True(Math.Abs(characters.GetSavedCharacter(GuestId)!.Items.Single(i => i.InstanceId == 77).Condition - 0.41f) < 0.001f);
		var transferred = items.GetTransferredItems(GuestId).Single(w => w.Item.InstanceId == 77);
		Assert.True(Math.Abs(transferred.Item.Condition - 0.41f) < 0.001f);
	}

	[Fact]
	public void Use_OfAFoodThatHandsTheEaterAReplacement_IsRefused()
	{
		// bucketofchicken's action instantiates an empty bucket and tries to put it in
		// the eater's hand: on the affected side that object would be created in the
		// EATER's world — a phantom item left at the parked spot plus the game's own
		// "too far" alert — so the family refuses the shape by name instead of running
		// it there. The item is untouched and no result reaches the kernel.
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, Item(42, "bucketofchicken", slot: 0)));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendUseRequest(HostId, 42);

		Assert.Empty(UseResults(received));
		Assert.Contains(characters.GetSavedCharacter(GuestId)!.Items, i => i.InstanceId == 42);
	}

	[Fact]
	public void Use_DeadTarget_IsRefused()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: false, alive: false));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, WaterBottle(42)));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendUseRequest(HostId, 42);

		Assert.DoesNotContain(KernelEvents(received), e => e.Kind == WireEventKind.PlayerItemUseResult);
		Assert.Contains(characters.GetSavedCharacter(GuestId)!.Items, i => i.InstanceId == 42);
	}

	[Fact]
	public void Use_UnknownItem_IsRefused()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, Item(42, "knife", slot: 0)));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendUseRequest(HostId, 42);

		Assert.DoesNotContain(KernelEvents(received), e => e.Kind == WireEventKind.PlayerItemUseResult);
		Assert.Contains(characters.GetSavedCharacter(GuestId)!.Items, i => i.InstanceId == 42);
	}

	[Fact]
	public void Use_ADrinkableInjectableContainer_IsRefusedByTheInjectionFirstOrder()
	{
		// The one-shot path's family chain asks the limb rules before the drink
		// rule, so a container the game marks BOTH ways (saline is usable and
		// usableOnLimb, and its liquid is injectable) stays the medical family's
		// business and this path refuses it by name — the routing order, not the
		// item's data, is what separates the two families, and this is the
		// production site where it is observable (ConsumeSemanticsTests pins only
		// that both rules answer for such a container).
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		var saline = MedicineBottle(42, "saline", "saline", amount: 750f);
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, saline));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendUseRequest(HostId, 42, doseMl: 100f);

		Assert.DoesNotContain(KernelEvents(received), e => e.Kind == WireEventKind.PlayerItemUseResult);
		Assert.Contains(characters.GetSavedCharacter(GuestId)!.Items, i => i.InstanceId == 42);
	}

	[Fact]
	public void Guest_UsesPaincreamOnHost_CommitsTheDoseAndCarriesItToThePatient()
	{
		// Part B of mod-cross-player-native-semantics: the operator's own client
		// measured 10 ml from paincream's own native limb action (`ApplyToLimb(limb,
		// 10f)`, Item.cs:650-653) and the request carries it. The host commits the
		// drain and carries the dose; the EFFECT belongs to the patient's client, so
		// the host computes no body state and writes none.
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var items = host.Services.GetRequiredService<IItemControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: true));
		var cream = TopicalBottle(42, "paincream", "reliefcream", amount: 100f);
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, cream));
		items.AdoptTransferredItem(GuestId, 42, cream);

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendUseRequest(HostId, 42, targetLimbIndex: 0, doseMl: 10f);

		var result = UseResult(received);
		Assert.Equal(GuestId, result.UserSteamId);
		Assert.Equal(HostId, result.TargetSteamId);
		Assert.Equal(42UL, result.ItemInstanceId);
		Assert.False(result.ItemDestroyed);
		Assert.NotNull(result.ItemAfter);
		Assert.True(Math.Abs(result.ItemAfter!.Condition - 0.9f) < 0.001f);

		var dose = Assert.Single(result.AppliedDose);
		Assert.Equal("reliefcream", dose.LiquidId);
		Assert.True(Math.Abs(dose.Amount - 10f) < 0.001f);

		// The host publishes no body snapshot for this family: the two display sinks
		// have no staleness guard, so echoing the patient's own pre-dose report back
		// would fight the effect the same message carries.
		Assert.Null(result.Health);
		Assert.Empty(result.Limbs);

		var hostData = characters.GetHostCharacterData()!;
		Assert.Equal(0f, hostData.Limbs[1].SkinHealAmount);
		Assert.Equal(0f, hostData.Limbs[1].DisinfectionTime);

		var saved = characters.GetSavedCharacter(GuestId)!.Items.Single(i => i.InstanceId == 42);
		Assert.True(Math.Abs(saved.Liquids.Single(l => l.LiquidId == "reliefcream").Amount - 90f) < 0.001f);
		var transferred = items.GetTransferredItems(GuestId).Single(w => w.Item.InstanceId == 42);
		Assert.True(Math.Abs(transferred.Item.Liquids.Single(l => l.LiquidId == "reliefcream").Amount - 90f) < 0.001f);
	}

	[Fact]
	public void Use_ATopicalRequestThatNamesNoLimb_IsRefused()
	{
		// A topical application IS the wound view's action: its native call site is
		// PlayerCamera.ApplyWoundItem's `ApplyToLimb(this.selectedLimb, 100f)`, and the
		// gesture that carries the operator's pick is the wound-view release
		// (RemoteMedicalOperationHandler validates the display limb and sends the
		// measured dose WITH it). The world drag is the inventory use — eat, drink,
		// wear — and carries no limb, so a topical request that names none is refused
		// instead of being resolved to whatever limb the patient's body happens to
		// hold (decision 246). Nothing is committed and no result reaches the kernel.
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var items = host.Services.GetRequiredService<IItemControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: true));
		var cream = TopicalBottle(42, "paincream", "reliefcream", amount: 100f);
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, cream));
		items.AdoptTransferredItem(GuestId, 42, cream);

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendUseRequest(HostId, 42, doseMl: 10f);

		Assert.DoesNotContain(KernelEvents(received), e => e.Kind == WireEventKind.PlayerItemUseResult);
		var saved = characters.GetSavedCharacter(GuestId)!.Items.Single(i => i.InstanceId == 42);
		Assert.True(Math.Abs(saved.Liquids.Single(l => l.LiquidId == "reliefcream").Amount - 100f) < 0.001f);
	}

	[Fact]
	public void Guest_UsesTopicalOnSelectedLimb_CarriesTheSelectionToThePatient()
	{
		// The limb choice moved to the side that owns the limb: the operator's pick
		// rides the result and the PATIENT resolves it against its own body (the
		// shared rule, pinned by InjectionSemanticsTests' limb case). The host neither
		// picks nor applies.
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var items = host.Services.GetRequiredService<IItemControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: true));
		var cream = TopicalBottle(42, "paincream", "reliefcream", amount: 100f);
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, cream));
		items.AdoptTransferredItem(GuestId, 42, cream);

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendUseRequest(HostId, 42, targetLimbIndex: 0, doseMl: 10f);

		var result = UseResult(received);
		Assert.Equal(GuestId, result.UserSteamId);
		Assert.Equal(HostId, result.TargetSteamId);
		Assert.Equal(42UL, result.ItemInstanceId);
		Assert.Equal(0, result.LimbIndex);
		Assert.Single(result.AppliedDose);

		var hostData = characters.GetHostCharacterData()!;
		Assert.Equal(0f, hostData.Limbs[0].SkinHealAmount);
		Assert.Equal(0f, hostData.Limbs[1].SkinHealAmount);
	}

	[Fact]
	public void Use_ATopicalRequestWithNoMeasuredDose_IsRefused()
	{
		// The dose is the operator's own measurement; a request that carries none
		// cannot become a draw, which is the native path's own early return for a
		// container with nothing to give. The limb is named so the request reaches THIS
		// refusal rather than the wound-view rule's (which fires first for the family).
		// The item is untouched and no result event reaches the kernel.
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: true));
		var cream = TopicalBottle(42, "paincream", "reliefcream", amount: 100f);
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, cream));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendUseRequest(HostId, 42, targetLimbIndex: 0);

		Assert.DoesNotContain(KernelEvents(received), e => e.Kind == WireEventKind.PlayerItemUseResult);
		var saved = characters.GetSavedCharacter(GuestId)!.Items.Single(i => i.InstanceId == 42);
		Assert.True(Math.Abs(saved.Liquids.Single(l => l.LiquidId == "reliefcream").Amount - 100f) < 0.001f);
	}

	[Fact]
	public void Use_AnItemNoChainClaims_IsRefused()
	{
		// `spraybottle` is a limb-drawable container the game cannot USE (its
		// `usable` flag is false) and `mystery` is neither injectable nor
		// health-usable, so no rule claims this item: the deleted catalogs refused
		// it by id and liquid allowlist, the game's own registries refuse it by
		// flag.
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		var bad = new CharacterItemMsg
		{
			InstanceId = 42,
			ItemId = "spraybottle",
			SlotIndex = 0,
			Condition = 1f,
			Liquids = [new LiquidStackMsg { LiquidId = "mystery", Amount = 100f }],
		};
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, bad));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendUseRequest(HostId, 42, targetLimbIndex: 0, doseMl: 10f);

		Assert.DoesNotContain(KernelEvents(received), e => e.Kind == WireEventKind.PlayerItemUseResult);
		Assert.Contains(characters.GetSavedCharacter(GuestId)!.Items, i => i.InstanceId == 42);
	}

	[Fact]
	public void Use_AContainerHoldingBothKinds_IsRefusedByTheInjectionFirstOrder()
	{
		// The one-shot path's family chain asks the injection rule before the
		// topical one, so a container holding BOTH kinds is the injection chain's
		// business and this path refuses it by name. That ordering is what makes
		// the two admission rules' overlap harmless, and this is the production
		// site where it is observable — TopicalSemanticsTests pins only that each
		// rule declines the other's family.
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: true));
		var mixed = new CharacterItemMsg
		{
			InstanceId = 42,
			ItemId = "paincream",
			SlotIndex = 0,
			Condition = 1f,
			Liquids =
			[
				new LiquidStackMsg { LiquidId = "reliefcream", Amount = 90f },
				new LiquidStackMsg { LiquidId = "fentanyl", Amount = 10f },
			],
		};
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, mixed));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendUseRequest(HostId, 42, targetLimbIndex: 0, doseMl: 10f);

		Assert.DoesNotContain(KernelEvents(received), e => e.Kind == WireEventKind.PlayerItemUseResult);
		var saved = characters.GetSavedCharacter(GuestId)!.Items.Single(i => i.InstanceId == 42);
		Assert.True(Math.Abs(saved.Liquids.Single(l => l.LiquidId == "reliefcream").Amount - 90f) < 0.001f);
		Assert.True(Math.Abs(saved.Liquids.Single(l => l.LiquidId == "fentanyl").Amount - 10f) < 0.001f);
	}
}
