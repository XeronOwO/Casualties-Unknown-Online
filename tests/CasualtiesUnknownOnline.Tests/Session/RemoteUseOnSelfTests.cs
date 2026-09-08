using System;
using System.Linq;
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
/// Behavior family: using a remote-held item on the owner (recursive container lookup, unconscious owner).
/// Split from PlayerInteractionServiceTests so xUnit v2 (one collection per class,
/// serial inside a class) does not serialize the whole player-interaction surface.
/// </summary>
[Trait("Category", "Integration")]
public class RemoteUseOnSelfTests
{
	[Fact]
	public void Host_UsesRemoteHeldGuestWaterOnSelf_AppliesToOwnerAndHost()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var items = host.Services.GetRequiredService<IItemControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: true));
		var water = WaterBottle(42);
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, water));
		items.AdoptTransferredItem(GuestId, 42, water);

		host.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendRemoteInventoryOperation(new RemoteInventoryOperationRequestMsg
			{
				Kind = RemoteInventoryOperationKind.UseOnSelf,
				OwnerSteamId = GuestId,
				ItemInstanceId = 42,
				TargetLimbIndex = -1,
			});

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
	}

	[Fact]
	public void Host_UsesRemoteHeldGuestItemInsideNestedContainerOnSelf_FindsRecursively()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var items = host.Services.GetRequiredService<IItemControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: true));
		var water = WaterBottle(42);
		var backpack = new CharacterItemMsg
		{
			InstanceId = 500,
			ItemId = "backpack",
			SlotIndex = 0,
			Contents = [water],
		};
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, backpack));
		items.AdoptTransferredItem(GuestId, 42, water);

		host.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendRemoteInventoryOperation(new RemoteInventoryOperationRequestMsg
			{
				Kind = RemoteInventoryOperationKind.UseOnSelf,
				OwnerSteamId = GuestId,
				ItemInstanceId = 42,
				TargetLimbIndex = -1,
			});

		var result = UseResult(received);
		Assert.Equal(GuestId, result.UserSteamId);
		Assert.Equal(HostId, result.TargetSteamId);
		Assert.Equal(42UL, result.ItemInstanceId);

		var saved = characters.GetSavedCharacter(GuestId)!.Items.Single(i => i.InstanceId == 500);
		var nested = Assert.Single(saved.Contents);
		Assert.Equal(42UL, nested.InstanceId);
		Assert.True(Math.Abs(nested.Liquids.Single(l => l.LiquidId == "water").Amount - 400f) < 0.001f);
	}

	[Fact]
	public void Guest_UsesRemoteHeldHostWaterOnSelf_ReverseDirectionWorks()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: true, alive: true, WaterBottle(77)));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendRemoteInventoryOperation(new RemoteInventoryOperationRequestMsg
			{
				Kind = RemoteInventoryOperationKind.UseOnSelf,
				OwnerSteamId = HostId,
				ItemInstanceId = 77,
				TargetLimbIndex = -1,
			});

		var result = UseResult(received);
		Assert.Equal(HostId, result.UserSteamId);
		Assert.Equal(GuestId, result.TargetSteamId);
		Assert.Equal(77UL, result.ItemInstanceId);

		var guestData = characters.GetSavedCharacter(GuestId)!;
		Assert.True(Math.Abs(guestData.Health!.Thirst - 9f) < 0.001f);
		var hostData = characters.GetHostCharacterData()!;
		var saved = Assert.Single(hostData.Items);
		Assert.True(Math.Abs(saved.Condition - 0.8f) < 0.001f);
	}

	[Fact]
	public void Host_UsesRemoteHeldItemFromUnconsciousGuest_AllowsOwnerUnconscious()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var items = host.Services.GetRequiredService<IItemControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: true));
		var water = WaterBottle(42);
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: false, water));
		items.AdoptTransferredItem(GuestId, 42, water);

		host.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendRemoteInventoryOperation(new RemoteInventoryOperationRequestMsg
			{
				Kind = RemoteInventoryOperationKind.UseOnSelf,
				OwnerSteamId = GuestId,
				ItemInstanceId = 42,
				TargetLimbIndex = -1,
			});

		var result = UseResult(received);
		Assert.Equal(GuestId, result.UserSteamId);
		Assert.Equal(HostId, result.TargetSteamId);
		var hostData = characters.GetHostCharacterData()!;
		Assert.True(Math.Abs(hostData.Health!.Thirst - 9f) < 0.001f);
	}

	[Fact]
	public void UseOnSelf_WornRemoteItem_IsRefused()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: true));
		// Worn items encode a negative SlotIndex; the remote item use path must
		// only operate on backpack/hand/container items, matching Take/Move/Pour.
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, Item(42, "bandage", slot: -2)));

		host.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendRemoteInventoryOperation(new RemoteInventoryOperationRequestMsg
			{
				Kind = RemoteInventoryOperationKind.UseOnSelf,
				OwnerSteamId = GuestId,
				ItemInstanceId = 42,
				TargetLimbIndex = -1,
			});

		Assert.DoesNotContain(KernelEvents(received), e => e.Kind == WireEventKind.PlayerItemUseResult);
		Assert.Contains(characters.GetSavedCharacter(GuestId)!.Items, i => i.InstanceId == 42);
	}
}
