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
public class MedicalToolApplicationTests
{
	[Fact]
	public void Guest_UsesBoneweldingToolOnHost_AppliesToolAndSendsResult()
	{
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

		var result = UseResult(received);
		Assert.Equal(GuestId, result.UserSteamId);
		Assert.Equal(HostId, result.TargetSteamId);
		Assert.Equal(42UL, result.ItemInstanceId);
		Assert.False(result.ItemDestroyed);
		Assert.NotNull(result.ItemAfter);
		Assert.True(Math.Abs(result.ItemAfter!.Condition - 0.25f) < 0.001f);

		var hostData = characters.GetHostCharacterData()!;
		Assert.True(Math.Abs(hostData.Health!.BloodViscosity - 7f) < 0.001f);
		Assert.True(Math.Abs(hostData.Limbs[1].BoneHealTimer - 25f) < 0.001f);
	}

	[Fact]
	public void Guest_UsesSplintOnHost_AppliesComponentAndDestroysItem()
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

		var result = UseResult(received);
		Assert.True(result.ItemDestroyed);
		Assert.Null(result.ItemAfter);

		var hostData = characters.GetHostCharacterData()!;
		Assert.True(hostData.Limbs[1].Splinted);
		var state = Assert.Single(hostData.Limbs[1].Components);
		Assert.Equal("SplintLimb", state.TypeName);
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
	public void Guest_UsesTourniquetOnHost_AppliesComponentAndDestroysItem()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var items = host.Services.GetRequiredService<IItemControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: true));
		var tourniquet = Item(42, "tourniquet", slot: 0);
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, tourniquet));
		items.AdoptTransferredItem(GuestId, 42, tourniquet);

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendUseRequest(HostId, 42);

		var result = UseResult(received);
		Assert.True(result.ItemDestroyed);
		Assert.Null(result.ItemAfter);

		var hostData = characters.GetHostCharacterData()!;
		Assert.True(hostData.Limbs[1].BlockedBleeding);
		var state = Assert.Single(hostData.Limbs[1].Components);
		Assert.Equal("TourniquetScript", state.TypeName);
		Assert.Empty(characters.GetSavedCharacter(GuestId)!.Items);
	}

	[Fact]
	public void Guest_UsesIcepackOnHost_AppliesComponentAndKeepsUsedItem()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var items = host.Services.GetRequiredService<IItemControl>();
		var hostSnapshot = SnapshotWithLimbs(HostId, conscious: true);
		hostSnapshot.Health!.Temperature = 37f;
		characters.SaveHostCharacterData(hostSnapshot);
		var icepack = Item(42, "icepack", slot: 0);
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, icepack));
		items.AdoptTransferredItem(GuestId, 42, icepack);

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendUseRequest(HostId, 42);

		var result = UseResult(received);
		Assert.False(result.ItemDestroyed);
		Assert.NotNull(result.ItemAfter);
		Assert.True(Math.Abs(result.ItemAfter!.Condition - 0.25f) < 0.001f);

		var hostData = characters.GetHostCharacterData()!;
		Assert.True(Math.Abs(hostData.Health!.Temperature - 36f) < 0.001f);
		var state = Assert.Single(hostData.Limbs[1].Components);
		Assert.Equal("ChilledLimb", state.TypeName);
		var saved = characters.GetSavedCharacter(GuestId)!.Items.Single(i => i.InstanceId == 42);
		Assert.True(Math.Abs(saved.Condition - 0.25f) < 0.001f);
		var transferred = items.GetTransferredItems(GuestId).Single(w => w.Item.InstanceId == 42);
		Assert.True(Math.Abs(transferred.Item.Condition - 0.25f) < 0.001f);
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
	public void Guest_UsesMedicalSutureOnHost_AppliesImmediateAndCarriesTimedEffect()
	{
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

		var result = UseResult(received);
		Assert.Equal(GuestId, result.UserSteamId);
		Assert.Equal(HostId, result.TargetSteamId);
		Assert.False(result.ItemDestroyed);
		Assert.NotNull(result.ItemAfter);
		Assert.True(Math.Abs(result.ItemAfter!.Condition - 0.24f) < 0.001f);

		var hostData = characters.GetHostCharacterData()!;
		Assert.True(Math.Abs(hostData.Limbs[1].Pain - 22.5f) < 0.001f);
		Assert.True(Math.Abs(hostData.Limbs[1].SkinHealAmount - 25f) < 0.001f);
		Assert.True(Math.Abs(hostData.Limbs[1].BleedAmount - 20f) < 0.001f);

		var timed = Assert.Single(result.TimedEffects);
		Assert.Equal(1, timed.LimbIndex);
		Assert.True(Math.Abs(timed.DurationSeconds - 10f) < 0.001f);
		Assert.True(Math.Abs(timed.BleedPerSecond + 4.5f) < 0.001f);
		Assert.Contains(characters.GetSavedCharacter(GuestId)!.Items, i => i.InstanceId == 42);
	}

	[Fact]
	public void Guest_UsesCombatPenOnHost_CarriesTimedBodyEffects()
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
		Assert.Equal(3, result.TimedBodyEffects.Count);
		Assert.Equal("highgradestimulant", result.TimedBodyEffects[0].EffectId);
		Assert.True(Math.Abs(result.TimedBodyEffects[0].DurationSeconds - 144f) < 0.001f);
		Assert.True(Math.Abs(result.TimedBodyEffects[1].DurationSeconds - 90f) < 0.001f);
		Assert.True(Math.Abs(result.TimedBodyEffects[2].DurationSeconds - 50f) < 0.001f);

		var saved = characters.GetSavedCharacter(GuestId)!.Items.Single(i => i.InstanceId == 42);
		Assert.Empty(saved.Liquids);
		var transferred = items.GetTransferredItems(GuestId).Single(w => w.Item.InstanceId == 42);
		Assert.Empty(transferred.Item.Liquids);
	}

	[Fact]
	public void Guest_UsesBloodCoagulantOnHost_CarriesTimedBodyEffect()
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
		// bloodcoagulant's native per-use amount is 33.334 ml; the medical
		// session commits exactly that and carries the scaled timed effect.
		ops.SendEndRequest(ack.OperationId, 33.334f);
		var result = Assert.Single(ends);
		Assert.NotNull(result.ItemAfter);
		Assert.True(Math.Abs(result.ItemAfter!.Liquids.Single().Amount - 66.666f) < 0.001f);

		var timedBody = Assert.Single(result.TimedBodyEffects);
		Assert.Equal("procoagulant", timedBody.EffectId);
		Assert.True(Math.Abs(timedBody.DurationSeconds - 20f) < 0.01f);
	}

	[Fact]
	public void Guest_UsesAntiradOnHost_CarriesTimedBodyEffectAndDrains()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var items = host.Services.GetRequiredService<IItemControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: true));
		var antirad = MedicineBottle(42, "antirad", "antirad", amount: 100f);
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, antirad));
		items.AdoptTransferredItem(GuestId, 42, antirad);

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendUseRequest(HostId, 42);

		var result = UseResult(received);
		Assert.Equal(GuestId, result.UserSteamId);
		Assert.Equal(HostId, result.TargetSteamId);
		Assert.False(result.ItemDestroyed);
		Assert.NotNull(result.ItemAfter);
		Assert.True(Math.Abs(result.ItemAfter!.Liquids.Single().Amount - 80f) < 0.001f);

		var timedBody = Assert.Single(result.TimedBodyEffects);
		Assert.Equal("antirad", timedBody.EffectId);
		Assert.True(Math.Abs(timedBody.DurationSeconds - 90f) < 0.001f);
		Assert.Empty(result.TimedEffects);
	}

	[Fact]
	public void Guest_UsesSleepingPillsOnHost_AddsComponentAmount()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var items = host.Services.GetRequiredService<IItemControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: true));
		var sleepingPills = MedicineBottle(42, "sleepingpills", "sleepingpills", amount: 25f);
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, sleepingPills));
		items.AdoptTransferredItem(GuestId, 42, sleepingPills);

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendUseRequest(HostId, 42);

		var result = UseResult(received);
		Assert.Equal(HostId, result.TargetSteamId);
		Assert.True(Math.Abs(result.Health!.SleepingPillsAmount - 300f) < 0.001f);

		var hostData = characters.GetHostCharacterData()!;
		Assert.True(Math.Abs(hostData.Health!.SleepingPillsAmount - 300f) < 0.001f);
	}

	[Fact]
	public void Guest_UsesMindwipeOnUnhappyHost_AppliesMindwipeScript()
	{
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
			.SendUseRequest(HostId, 42);

		var result = UseResult(received);
		Assert.True(result.Health!.MindwipeScriptPresent);
		Assert.False(result.Health.MindwipeScriptActive);

		var hostData = characters.GetHostCharacterData()!;
		Assert.True(hostData.Health!.MindwipeScriptPresent);

		var authority = host.Services.GetRequiredService<ItemKernelAuthority>();
		var player = authority.QueryPlayers()!.Players.Single(p => p.SteamId == HostId);
		Assert.NotNull(player.Body);
		Assert.True(player.Body!.MindwipeScriptPresent);
		Assert.False(player.Body!.MindwipeScriptActive);
	}

	[Fact]
	public void Use_MindwipeOnMentallyHealthyHost_IsRefused()
	{
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
