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
	public void Guest_UsesWaterOnHost_AppliesDrinkAndSendsResult()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var items = host.Services.GetRequiredService<IItemControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: true));
		var water = WaterBottle(42);
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, water));
		items.AdoptTransferredItem(GuestId, 42, water);

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendUseRequest(HostId, 42);

		var result = UseResult(received);
		Assert.Equal(GuestId, result.UserSteamId);
		Assert.Equal(HostId, result.TargetSteamId);
		Assert.Equal(42UL, result.ItemInstanceId);
		Assert.False(result.ItemDestroyed);
		Assert.NotNull(result.ItemAfter);
		Assert.True(Math.Abs(result.ItemAfter!.Condition - 0.8f) < 0.001f);

		var hostData = characters.GetHostCharacterData()!;
		Assert.True(Math.Abs(hostData.Health!.Thirst - 9f) < 0.001f);
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
			.SendUseRequest(HostId, 42);

		Assert.NotNull(hostUse);
		Assert.Equal(GuestId, hostUse!.UserSteamId);
		Assert.Equal(HostId, hostUse.TargetSteamId);
		Assert.NotNull(guestUse);
		Assert.Equal(GuestId, guestUse!.UserSteamId);
		Assert.Equal(HostId, guestUse.TargetSteamId);
	}

	[Fact]
	public void Host_UsesBreadOnGuest_AppliesFoodAndSendsResult()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true, Item(77, "bread", slot: 0)));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		host.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendUseRequest(GuestId, 77);

		var result = UseResult(received);
		Assert.Equal(HostId, result.UserSteamId);
		Assert.Equal(GuestId, result.TargetSteamId);
		Assert.False(result.ItemDestroyed);
		Assert.True(Math.Abs(result.ItemAfter!.Condition - 0.41f) < 0.001f);

		var guestData = characters.GetSavedCharacter(GuestId)!;
		Assert.True(Math.Abs(guestData.Health!.Hunger - 9f) < 0.001f);
		var hostData = characters.GetHostCharacterData()!;
		Assert.True(Math.Abs(hostData.Items.Single(i => i.InstanceId == 77).Condition - 0.41f) < 0.001f);

		var authority = host.Services.GetRequiredService<ItemKernelAuthority>();
		var kernelItem = authority.FindItem(77);
		Assert.NotNull(kernelItem);
		Assert.Equal(ItemLocationKind.Carried, kernelItem!.Value.Location.Kind);
		Assert.Equal(HostId, kernelItem.Value.Location.Owner.Value);
		Assert.True(Math.Abs(kernelItem.Value.Data.Condition - 0.41f) < 0.001f);

		// The guest's replay kernel receives the same post-use item fact through
		// KernelEnvelope.
		var guestAuthority = guest.Services.GetRequiredService<ItemKernelAuthority>();
		var guestKernelItem = guestAuthority.FindItem(77);
		Assert.NotNull(guestKernelItem);
		Assert.Equal(ItemLocationKind.Carried, guestKernelItem!.Value.Location.Kind);
		Assert.Equal(HostId, guestKernelItem.Value.Location.Owner.Value);
		Assert.True(Math.Abs(guestKernelItem.Value.Data.Condition - 0.41f) < 0.001f);
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
	public void Use_UnknownMedicineLiquid_IsRefused()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		var bad = new CharacterItemMsg
		{
			InstanceId = 42,
			ItemId = "saline",
			SlotIndex = 0,
			Condition = 1f,
			Liquids = [new LiquidStackMsg { LiquidId = "mystery", Amount = 750f }],
		};
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, bad));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendUseRequest(HostId, 42);

		Assert.DoesNotContain(KernelEvents(received), e => e.Kind == WireEventKind.PlayerItemUseResult);
		Assert.Contains(characters.GetSavedCharacter(GuestId)!.Items, i => i.InstanceId == 42);
	}

	[Fact]
	public void Guest_UsesPaincreamOnHost_AppliesTopicalAndSendsResult()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var items = host.Services.GetRequiredService<IItemControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: true));
		var cream = TopicalBottle(42, "paincream", "reliefcream", amount: 100f);
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, cream));
		items.AdoptTransferredItem(GuestId, 42, cream);

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendUseRequest(HostId, 42);

		var result = UseResult(received);
		Assert.Equal(GuestId, result.UserSteamId);
		Assert.Equal(HostId, result.TargetSteamId);
		Assert.Equal(42UL, result.ItemInstanceId);
		Assert.False(result.ItemDestroyed);
		Assert.NotNull(result.ItemAfter);
		Assert.True(Math.Abs(result.ItemAfter!.Condition - 0.9f) < 0.001f);

		var hostData = characters.GetHostCharacterData()!;
		var limb = hostData.Limbs[1];
		Assert.True(Math.Abs(limb.SkinHealAmount - 3f) < 0.001f);
		Assert.True(Math.Abs(limb.DisinfectionTime - 300f) < 0.001f);

		var saved = characters.GetSavedCharacter(GuestId)!.Items.Single(i => i.InstanceId == 42);
		Assert.True(Math.Abs(saved.Liquids.Single(l => l.LiquidId == "reliefcream").Amount - 90f) < 0.001f);
		var transferred = items.GetTransferredItems(GuestId).Single(w => w.Item.InstanceId == 42);
		Assert.True(Math.Abs(transferred.Item.Liquids.Single(l => l.LiquidId == "reliefcream").Amount - 90f) < 0.001f);
	}

	[Fact]
	public void Guest_UsesTopicalOnSelectedLimb_AppliesRequestedLimbNotAutoPick()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var items = host.Services.GetRequiredService<IItemControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: true));
		var cream = TopicalBottle(42, "paincream", "reliefcream", amount: 100f);
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, cream));
		items.AdoptTransferredItem(GuestId, 42, cream);

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendUseRequest(HostId, 42, targetLimbIndex: 0);

		var result = UseResult(received);
		Assert.Equal(GuestId, result.UserSteamId);
		Assert.Equal(HostId, result.TargetSteamId);
		Assert.Equal(42UL, result.ItemInstanceId);

		var hostData = characters.GetHostCharacterData()!;
		Assert.True(Math.Abs(hostData.Limbs[0].SkinHealAmount - 3f) < 0.001f);
		Assert.True(Math.Abs(hostData.Limbs[0].DisinfectionTime - 300f) < 0.001f);
		Assert.Equal(0f, hostData.Limbs[1].SkinHealAmount);
		Assert.Equal(0f, hostData.Limbs[1].DisinfectionTime);
	}

	[Fact]
	public void Use_UnknownTopicalLiquid_IsRefused()
	{
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
			.SendUseRequest(HostId, 42);

		Assert.DoesNotContain(KernelEvents(received), e => e.Kind == WireEventKind.PlayerItemUseResult);
		Assert.Contains(characters.GetSavedCharacter(GuestId)!.Items, i => i.InstanceId == 42);
	}
}
