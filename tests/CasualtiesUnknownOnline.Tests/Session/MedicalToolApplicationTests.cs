using System;
using System.Collections.Generic;
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
/// Behavior family: applying medical tools and timed-effect medicines to another player.
/// Split from PlayerInteractionServiceTests so xUnit v2 (one collection per class,
/// serial inside a class) does not serialize the whole player-interaction surface.
/// </summary>
[Trait("Category", "Integration")]
public class MedicalToolApplicationTests
{
	[Fact]
	public void Guest_UsesBoneweldingToolOnHost_AdmitsItAndChangesNothing()
	{
		// The limb tool is the fifth migrated family: the host's whole part of the
		// request is the admission, because the AFFECTED side's own client runs the
		// item's own useLimbAction against its own limb. The deleted catalog's numbers
		// (condition cost 0.5, skin -25, muscle -26, pain +30, bleed +5, bone-heal timer
		// x0.25, blood viscosity +2) used to be applied to this snapshot right here.
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var items = host.Services.GetRequiredService<IItemControl>();
		var hostSnapshot = SnapshotWithLimbs(HostId, conscious: true);
		hostSnapshot.Health!.BloodViscosity = 5f;
		hostSnapshot.Limbs[1].BoneHealTimer = 100f;
		characters.SaveHostCharacterData(hostSnapshot);
		var tool = Item(42, "boneweldingtool", slot: 0);
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, tool));
		items.AdoptTransferredItem(GuestId, 42, tool);

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendUseRequest(HostId, 42);

		var request = Assert.Single(UseResults(received));
		Assert.Equal(GuestId, request.UserSteamId);
		Assert.Equal(HostId, request.TargetSteamId);
		Assert.Equal(42UL, request.ItemInstanceId);
		Assert.True(request.TargetRunsLimbAction);
		Assert.False(request.TargetEatsTheItem);
		Assert.False(request.ItemDestroyed);
		Assert.Null(request.ItemAfter);
		Assert.Null(request.Health);
		Assert.Empty(request.Limbs);

		var untouched = characters.GetHostCharacterData()!;
		Assert.True(Math.Abs(untouched.Health!.BloodViscosity - 5f) < 0.001f);
		Assert.True(Math.Abs(untouched.Limbs[1].BoneHealTimer - 100f) < 0.001f);
		var carried = characters.GetSavedCharacter(GuestId)!.Items.Single(i => i.InstanceId == 42);
		Assert.True(Math.Abs(carried.Condition - 0.75f) < 0.001f);
	}

	[Fact]
	public void AffectedSideOutcome_CommitsTheConditionTheOwnActionLeft()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var items = host.Services.GetRequiredService<IItemControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: true));
		var tool = Item(42, "boneweldingtool", slot: 0);
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, tool));
		items.AdoptTransferredItem(GuestId, 42, tool);

		var interactions = guest.Services.GetRequiredService<IPlayerInteractionControl>();
		interactions.SendUseRequest(HostId, 42);

		// The treated player's client ran the delegate and reports what the item became.
		// The host is the affected side here, so its own client files the report.
		host.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendItemActionOutcome(42, 0.25f, consumed: false);

		var results = UseResults(received);
		Assert.Equal(2, results.Count);
		var committed = results[1];
		Assert.Equal(GuestId, committed.UserSteamId);
		Assert.Equal(HostId, committed.TargetSteamId);
		Assert.False(committed.ItemDestroyed);
		Assert.NotNull(committed.ItemAfter);
		Assert.True(Math.Abs(committed.ItemAfter!.Condition - 0.25f) < 0.001f);

		var saved = characters.GetSavedCharacter(GuestId)!.Items.Single(i => i.InstanceId == 42);
		Assert.True(Math.Abs(saved.Condition - 0.25f) < 0.001f);
		var transferred = items.GetTransferredItems(GuestId).Single(w => w.Item.InstanceId == 42);
		Assert.True(Math.Abs(transferred.Item.Condition - 0.25f) < 0.001f);
	}

	[Fact]
	public void AffectedSideOutcome_ConsumedComponentTool_RemovesTheOwnersRow()
	{
		// The component-bearing tools turn the item into the limb component and destroy
		// the object they were handed (Item.cs:1489 for the splint), which is the one
		// thing the tool's own data cannot say — hence the report's own flag.
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var items = host.Services.GetRequiredService<IItemControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: true));
		var splint = Item(42, "splint", slot: 0);
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, splint));
		items.AdoptTransferredItem(GuestId, 42, splint);

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendUseRequest(HostId, 42);
		host.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendItemActionOutcome(42, 0f, consumed: true);

		var committed = UseResults(received)[1];
		Assert.True(committed.ItemDestroyed);
		Assert.Null(committed.ItemAfter);
		Assert.Empty(characters.GetSavedCharacter(GuestId)!.Items);
		Assert.Empty(items.GetTransferredItems(GuestId));

		var hostAuthority = host.Services.GetRequiredService<ItemKernelAuthority>();
		var hostItem = hostAuthority.FindItem(42);
		Assert.NotNull(hostItem);
		Assert.NotEqual(ItemLocationKind.Carried, hostItem!.Value.Location.Kind);
		var guestAuthority = guest.Services.GetRequiredService<ItemKernelAuthority>();
		var guestItem = guestAuthority.FindItem(42);
		Assert.NotNull(guestItem);
		Assert.NotEqual(ItemLocationKind.Carried, guestItem!.Value.Location.Kind);
	}

	[Fact]
	public void AffectedSideOutcome_WithoutAnAdmittedUse_IsRefused()
	{
		// The admission IS the grant: without it the report would be one member writing
		// another member's carried item.
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: true));
		var splint = Item(42, "splint", slot: 0);
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, splint));

		host.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendItemActionOutcome(42, 0.4f, consumed: false);

		Assert.Empty(UseResults(received));
		Assert.True(Math.Abs(characters.GetSavedCharacter(GuestId)!.Items.Single(i => i.InstanceId == 42).Condition - 0.75f) < 0.001f);
	}

	[Fact]
	public void AffectedSideOutcome_Twice_SettlesTheItemOnce()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var items = host.Services.GetRequiredService<IItemControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: true));
		var icepack = Item(42, "icepack", slot: 0);
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, icepack));
		items.AdoptTransferredItem(GuestId, 42, icepack);

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendUseRequest(HostId, 42);
		var interactions = host.Services.GetRequiredService<IPlayerInteractionControl>();
		interactions.SendItemActionOutcome(42, 0.5f, consumed: false);
		interactions.SendItemActionOutcome(42, 0.1f, consumed: false);

		var results = UseResults(received);
		Assert.Equal(2, results.Count);
		var saved = characters.GetSavedCharacter(GuestId)!.Items.Single(i => i.InstanceId == 42);
		Assert.True(Math.Abs(saved.Condition - 0.5f) < 0.001f);
	}

	[Fact]
	public void Guest_UsesSplintOnHost_IsAdmittedWithoutSpendingTheItem()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var items = host.Services.GetRequiredService<IItemControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: true));
		var splint = Item(42, "splint", slot: 0);
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, splint));
		items.AdoptTransferredItem(GuestId, 42, splint);

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendUseRequest(HostId, 42);

		var request = Assert.Single(UseResults(received));
		Assert.True(request.TargetRunsLimbAction);
		Assert.False(request.ItemDestroyed);
		Assert.Null(request.ItemAfter);

		// Nothing ran on the host: no component, no limb latch, and the item is still
		// the guest's carried row until the affected side reports what its own run left.
		var hostData = characters.GetHostCharacterData()!;
		Assert.False(hostData.Limbs[1].Splinted);
		Assert.Empty(hostData.Limbs[1].Components);
		Assert.Single(characters.GetSavedCharacter(GuestId)!.Items);
		Assert.Single(items.GetTransferredItems(GuestId));
	}

	[Fact]
	public void DirectUseRequest_WithTweezers_IsRefused_AndShrapnelStays()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var hostSnapshot = SnapshotWithLimbs(HostId, conscious: true);
		hostSnapshot.Limbs[1].Shrapnel = 3;
		characters.SaveHostCharacterData(hostSnapshot);
		var tweezers = Item(42, "tweezers", slot: 0);
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, tweezers));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendUseRequest(HostId, 42);

		// The one-shot tweezers path is gone; it must not emit a use result or
		// mutate the authoritative limb. Shrapnel goes through the shared session.
		Assert.DoesNotContain(KernelEvents(received), e => e.Kind == WireEventKind.PlayerItemUseResult);
		var hostData = characters.GetHostCharacterData()!;
		Assert.Equal(3, hostData.Limbs[1].Shrapnel);
		Assert.Contains(characters.GetSavedCharacter(GuestId)!.Items, i => i.InstanceId == 42);
	}

	[Fact]
	public void Tweezers_NoShrapnelOnTarget_IsRefused()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: true));
		var tweezers = Item(42, "tweezers", slot: 0);
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, tweezers));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendUseRequest(HostId, 42);

		Assert.DoesNotContain(KernelEvents(received), e => e.Kind == WireEventKind.PlayerItemUseResult);
		Assert.Contains(characters.GetSavedCharacter(GuestId)!.Items, i => i.InstanceId == 42);
	}

	[Fact]
	public void Guest_UsesMedicalSutureOnHost_AdmitsItWithoutAHostTimedEffect()
	{
		// medicalsuture is the tool whose delegate starts a timed op of its own
		// (`CoUtils.DoTimedOp("suture" + limb.name, …)`, Item.cs:381-384), keyed by the
		// limb's own name so repeated doses accumulate. That ramp, the immediate
		// pain/skin-heal and the item's condition cost all run inside the delegate on
		// the treated player's client now, so no TimedLimbEffectMsg exists any more.
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var items = host.Services.GetRequiredService<IItemControl>();
		var hostSnapshot = SnapshotWithLimbs(HostId, conscious: true);
		hostSnapshot.Limbs[1].Pain = 10f;
		hostSnapshot.Limbs[1].BleedAmount = 20f;
		characters.SaveHostCharacterData(hostSnapshot);
		var suture = Item(42, "medicalsuture", slot: 0);
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, suture));
		items.AdoptTransferredItem(GuestId, 42, suture);

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendUseRequest(HostId, 42);

		var request = Assert.Single(UseResults(received));
		Assert.Equal(GuestId, request.UserSteamId);
		Assert.Equal(HostId, request.TargetSteamId);
		Assert.True(request.TargetRunsLimbAction);
		Assert.False(request.ItemDestroyed);
		Assert.Null(request.ItemAfter);

		var untouched = characters.GetHostCharacterData()!;
		Assert.True(Math.Abs(untouched.Limbs[1].Pain - 10f) < 0.001f);
		Assert.True(Math.Abs(untouched.Limbs[1].SkinHealAmount) < 0.001f);
		Assert.True(Math.Abs(untouched.Limbs[1].BleedAmount - 20f) < 0.001f);
		Assert.Contains(characters.GetSavedCharacter(GuestId)!.Items, i => i.InstanceId == 42);
	}

	[Fact]
	public void Guest_UsesCombatPenOnHost_CarriesTheWholeDoseInTheTerminal()
	{
		var (host, guest, _) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var items = host.Services.GetRequiredService<IItemControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: true));
		var pen = new CharacterItemMsg
		{
			InstanceId = 42,
			ItemId = "combatpen",
			SlotIndex = 0,
			Condition = 1f,
			Liquids =
			[
				new LiquidStackMsg { LiquidId = "highgradestimulant", Amount = 60f },
				new LiquidStackMsg { LiquidId = "epinephrine", Amount = 15f },
				new LiquidStackMsg { LiquidId = "oxyline", Amount = 25f },
			],
		};
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, pen));
		items.AdoptTransferredItem(GuestId, 42, pen);

		MedicalOperationStartAckMsg? ack = null;
		var ends = new List<MedicalOperationEndCommittedMsg>();
		var ops = guest.Services.GetRequiredService<IPlayerInteractionControl>().MedicalOperations;
		ops.StartAckReceived += m => ack = m;
		ops.EndCommittedReceived += ends.Add;

		ops.SendStartRequest(HostId, 42, -1);
		Assert.NotNull(ack);
		Assert.True(ack!.Accepted);
		ops.SendEndRequest(ack.OperationId, 100f);
		var result = Assert.Single(ends);
		Assert.Equal(MedicalOperationTerminalReason.Completed, result.TerminalReason);
		Assert.NotNull(result.ItemAfter);
		Assert.Empty(result.ItemAfter!.Liquids);

		// The one unreported delta's dose: the whole drained plan, and the
		// patient's own client turns each of the three liquids into the game's
		// own injection effect — no CUO duration or per-ml coefficient travels.
		Assert.Equal(3, result.AppliedDose.Count);
		Assert.Equal("highgradestimulant", result.AppliedDose[0].LiquidId);
		Assert.True(Math.Abs(result.AppliedDose[0].Amount - 60f) < 0.001f);
		Assert.Equal("epinephrine", result.AppliedDose[1].LiquidId);
		Assert.True(Math.Abs(result.AppliedDose[1].Amount - 15f) < 0.001f);
		Assert.Equal("oxyline", result.AppliedDose[2].LiquidId);
		Assert.True(Math.Abs(result.AppliedDose[2].Amount - 25f) < 0.001f);

		var saved = characters.GetSavedCharacter(GuestId)!.Items.Single(i => i.InstanceId == 42);
		Assert.Empty(saved.Liquids);
		var transferred = items.GetTransferredItems(GuestId).Single(w => w.Item.InstanceId == 42);
		Assert.Empty(transferred.Item.Liquids);
	}

	[Fact]
	public void Guest_UsesBloodCoagulantOnHost_CarriesTheDoseInTheTerminal()
	{
		var (host, guest, _) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var items = host.Services.GetRequiredService<IItemControl>();
		var hostSnapshot = SnapshotWithLimbs(HostId, conscious: true);
		hostSnapshot.Health!.BloodViscosity = 10f;
		characters.SaveHostCharacterData(hostSnapshot);
		var coagulant = MedicineBottle(42, "bloodcoagulant", "procoagulant", amount: 100f);
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, coagulant));
		items.AdoptTransferredItem(GuestId, 42, coagulant);

		MedicalOperationStartAckMsg? ack = null;
		var ends = new List<MedicalOperationEndCommittedMsg>();
		var ops = guest.Services.GetRequiredService<IPlayerInteractionControl>().MedicalOperations;
		ops.StartAckReceived += m => ack = m;
		ops.EndCommittedReceived += ends.Add;

		ops.SendStartRequest(HostId, 42, -1);
		Assert.NotNull(ack);
		Assert.True(ack!.Accepted);
		// The native delegate's own per-use amount (33.334 ml) is what the
		// session commits; the plan it produces is the procoagulant draw, and
		// the timed body itself starts on the patient's client.
		ops.SendEndRequest(ack.OperationId, 33.334f);
		var result = Assert.Single(ends);
		Assert.NotNull(result.ItemAfter);
		Assert.True(Math.Abs(result.ItemAfter!.Liquids.Single().Amount - 66.666f) < 0.001f);

		var dose = Assert.Single(result.AppliedDose);
		Assert.Equal("procoagulant", dose.LiquidId);
		Assert.True(Math.Abs(dose.Amount - 33.334f) < 0.01f);
	}

	[Fact]
	public void Guest_UsesAntiradOnHost_CarriesTheMeasuredDrinkDose()
	{
		// antirad is a DRINK, not a limb treatment: the item's own use action is
		// `Drink(body, 20f, "pills")` (Item.cs:1399), so the operator's client
		// measured 20 ml and the request carries it. The host commits the drain and
		// carries it as DrinkDose; the timed radiation tick the deleted
		// TimedBodyEffect row used to schedule is now the liquid's own onDrink body,
		// run on the patient's client.
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var items = host.Services.GetRequiredService<IItemControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: true));
		var antirad = MedicineBottle(42, "antirad", "antirad", amount: 100f);
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, antirad));
		items.AdoptTransferredItem(GuestId, 42, antirad);

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendUseRequest(HostId, 42, doseMl: 20f);

		var result = UseResult(received);
		Assert.Equal(GuestId, result.UserSteamId);
		Assert.Equal(HostId, result.TargetSteamId);
		Assert.False(result.ItemDestroyed);
		Assert.NotNull(result.ItemAfter);
		Assert.True(Math.Abs(result.ItemAfter!.Liquids.Single().Amount - 80f) < 0.001f);

		var dose = Assert.Single(result.DrinkDose);
		Assert.Equal("antirad", dose.LiquidId);
		Assert.True(Math.Abs(dose.Amount - 20f) < 0.001f);
		Assert.Null(result.Health);
	}

	[Fact]
	public void Guest_UsesSleepingPillsOnHost_CarriesTheMeasuredDrinkDose()
	{
		// sleepingpills drinks 5 ml (`Drink(body, 5f, "pills")`, Item.cs:1422) and
		// the component dose is the liquid's own onDrink business on the patient's
		// client, so the host carries the drain and no body state.
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var items = host.Services.GetRequiredService<IItemControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: true));
		var sleepingPills = MedicineBottle(42, "sleepingpills", "sleepingpills", amount: 25f);
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, sleepingPills));
		items.AdoptTransferredItem(GuestId, 42, sleepingPills);

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendUseRequest(HostId, 42, doseMl: 5f);

		var result = UseResult(received);
		Assert.Equal(HostId, result.TargetSteamId);
		Assert.Null(result.Health);
		var dose = Assert.Single(result.DrinkDose);
		Assert.Equal("sleepingpills", dose.LiquidId);
		Assert.True(Math.Abs(dose.Amount - 5f) < 0.001f);

		var hostData = characters.GetHostCharacterData()!;
		Assert.Equal(0f, hostData.Health!.SleepingPillsAmount);
	}

	[Fact]
	public void Guest_UsesMindwipeOnHost_CarriesTheMeasuredDrinkDose()
	{
		// mindwipe's item-level health gate lives in the item's OWN delegate
		// (Item.cs:1343-1352) and runs on the operator's client against the
		// affected player's own body, so a healthy target delivers NO dose and the
		// host refuses the request; this case is the admitted half. The liquid's own
		// second gate (`MindwipeScript` already present) and the script it adds run
		// on the patient's client through onDrink.
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var items = host.Services.GetRequiredService<IItemControl>();
		var hostSnapshot = SnapshotWithLimbs(HostId, conscious: true);
		hostSnapshot.Health!.Happiness = -60f;
		hostSnapshot.Health.BrainHealth = 50f;
		hostSnapshot.Health.StrokeAmount = 10f;
		characters.SaveHostCharacterData(hostSnapshot);
		var mindwipe = MedicineBottle(42, "mindwipe", "mindwipe", amount: 60f);
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, mindwipe));
		items.AdoptTransferredItem(GuestId, 42, mindwipe);

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendUseRequest(HostId, 42, doseMl: 60f);

		var result = UseResult(received);
		Assert.Null(result.Health);
		var dose = Assert.Single(result.DrinkDose);
		Assert.Equal("mindwipe", dose.LiquidId);
		Assert.True(Math.Abs(dose.Amount - 60f) < 0.001f);

		var hostData = characters.GetHostCharacterData()!;
		Assert.False(hostData.Health!.MindwipeScriptPresent);
	}

	[Fact]
	public void Use_ADrinkRequestWithNoMeasuredDose_IsRefused()
	{
		// The item-level gate that used to be mirrored here is now the item's own
		// delegate on the operator's client: a refused drink reaches no
		// WaterContainerItem.Drink call, so the request carries no dose — the same
		// shape the topical chain's no-dose refusal has. The host cannot re-judge
		// it: the gate's inputs are the patient's, and the patient's own body is
		// where the liquid-level gate still runs.
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var hostSnapshot = Snapshot(HostId, conscious: true);
		hostSnapshot.Health!.Happiness = 0f;
		hostSnapshot.Health.BrainHealth = 95f;
		hostSnapshot.Health.StrokeAmount = 0f;
		characters.SaveHostCharacterData(hostSnapshot);
		var mindwipe = MedicineBottle(42, "mindwipe", "mindwipe", amount: 60f);
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, mindwipe));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendUseRequest(HostId, 42);

		Assert.DoesNotContain(KernelEvents(received), e => e.Kind == WireEventKind.PlayerItemUseResult);
		Assert.Contains(characters.GetSavedCharacter(GuestId)!.Items, i => i.InstanceId == 42);
	}
}
