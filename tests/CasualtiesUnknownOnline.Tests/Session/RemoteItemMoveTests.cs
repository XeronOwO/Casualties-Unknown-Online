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
/// Behavior family: moving/dropping another player's carried item (drop, container, nested container, slot, pour).
/// Split from PlayerInteractionServiceTests so xUnit v2 (one collection per class,
/// serial inside a class) does not serialize the whole player-interaction surface.
/// </summary>
public class RemoteItemMoveTests
{
	[Fact]
	public void Host_DirectRemoteDrop_Works()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true, Item(42)));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		host.Services.GetRequiredService<IPlayerInteractionControl>()
			.HandleRemoteInventoryOperation(GuestId, new RemoteInventoryOperationRequestMsg
			{
				Kind = RemoteInventoryOperationKind.Drop,
				OwnerSteamId = HostId,
				ItemInstanceId = 42,
			});

		Assert.Empty(characters.GetHostCharacterData()!.Items);
		Assert.Contains(KernelEvents(received), e =>
			(e.Kind == WireEventKind.ItemRelocated || e.Kind == WireEventKind.ItemSpawned)
			&& e.Identity.InstanceId == 42
			&& e.NewLocation?.Kind == WireItemLocationKind.World);
	}

	[Fact]
	public void Guest_DropsRemotePlayersItem_MovesKernelToWorldAndTellsOwnerToRemove()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true, Item(42)));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendRemoteInventoryOperation(new RemoteInventoryOperationRequestMsg
			{
				Kind = RemoteInventoryOperationKind.Drop,
				OwnerSteamId = HostId,
				ItemInstanceId = 42,
			});

		var transfer = TransferResult(received);
		Assert.Equal(HostId, transfer.FromSteamId);
		Assert.Equal(0UL, transfer.ToSteamId);
		Assert.Equal(42UL, transfer.Item!.InstanceId);

		Assert.DoesNotContain(characters.GetHostCharacterData()!.Items, i => i.InstanceId == 42);

		var authority = host.Services.GetRequiredService<ItemKernelAuthority>();
		var kernelItem = authority.FindItem(42);
		Assert.NotNull(kernelItem);
		Assert.Equal(ItemLocationKind.World, kernelItem!.Value.Location.Kind);
		Assert.Contains(KernelEvents(received), e =>
			(e.Kind == WireEventKind.ItemRelocated || e.Kind == WireEventKind.ItemSpawned)
			&& e.Identity.InstanceId == 42
			&& e.NewLocation?.Kind == WireItemLocationKind.World);
	}

	[Fact]
	public void Guest_MovesRemotePlayersItemIntoRemoteContainer_UpdatesTreeAndSendsParentTransfer()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var backpack = new CharacterItemMsg
		{
			InstanceId = 500,
			ItemId = "backpack",
			SlotIndex = 0,
		};
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true, backpack, Item(42)));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendRemoteInventoryOperation(new RemoteInventoryOperationRequestMsg
			{
				Kind = RemoteInventoryOperationKind.MoveToContainer,
				OwnerSteamId = HostId,
				ItemInstanceId = 42,
				TargetContainerInstanceId = 500,
			});

		var transfer = TransferResult(received);
		Assert.Equal(HostId, transfer.FromSteamId);
		Assert.Equal(HostId, transfer.ToSteamId);
		Assert.Equal(500UL, transfer.TargetParentItemId);
		Assert.Equal(42UL, transfer.Item!.InstanceId);

		var hostData = characters.GetHostCharacterData()!;
		var remainingBackpack = Assert.Single(hostData.Items);
		Assert.Equal(500UL, remainingBackpack.InstanceId);
		Assert.Contains(remainingBackpack.Contents, i => i.InstanceId == 42);

		var authority = host.Services.GetRequiredService<ItemKernelAuthority>();
		var kernelItem = authority.FindItem(42);
		Assert.NotNull(kernelItem);
		Assert.Equal(ItemLocationKind.Contained, kernelItem!.Value.Location.Kind);
		Assert.Equal(500UL, kernelItem.Value.Location.ParentItemId);
	}

	[Fact]
	public void Guest_MovesRemotePlayersItemIntoNestedRemoteContainer_UpdatesDeepTreeAndSendsParentTransfer()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var inner = new CharacterItemMsg
		{
			InstanceId = 501,
			ItemId = "innerbox",
			SlotIndex = 0,
		};
		var backpack = new CharacterItemMsg
		{
			InstanceId = 500,
			ItemId = "backpack",
			SlotIndex = 0,
			Contents = [inner],
		};
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true, backpack, Item(42, "waterbottle")));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendRemoteInventoryOperation(new RemoteInventoryOperationRequestMsg
			{
				Kind = RemoteInventoryOperationKind.MoveToContainer,
				OwnerSteamId = HostId,
				ItemInstanceId = 42,
				TargetContainerInstanceId = 501,
			});

		var transfer = TransferResult(received);
		Assert.Equal(HostId, transfer.FromSteamId);
		Assert.Equal(HostId, transfer.ToSteamId);
		Assert.Equal(501UL, transfer.TargetParentItemId);
		Assert.Equal(42UL, transfer.Item!.InstanceId);

		var hostData = characters.GetHostCharacterData()!;
		var remainingBackpack = Assert.Single(hostData.Items);
		var remainingInner = Assert.Single(remainingBackpack.Contents);
		Assert.Equal(501UL, remainingInner.InstanceId);
		Assert.Contains(remainingInner.Contents, i => i.InstanceId == 42);

		var authority = host.Services.GetRequiredService<ItemKernelAuthority>();
		var kernelItem = authority.FindItem(42);
		Assert.NotNull(kernelItem);
		Assert.Equal(ItemLocationKind.Contained, kernelItem!.Value.Location.Kind);
		Assert.Equal(501UL, kernelItem.Value.Location.ParentItemId);

		// The nested target container itself must remain Contained under the
		// top-level backpack. If SyncContainerItemsCommand is issued against the
		// nested node and that node is absent from the kernel, the old code
		// spawned it as a Carried root, which made EmitCarriedFactsForBatch lift
		// the trash bag to the clone's top level (visible→invisible cycle).
		var innerKernel = authority.FindItem(501);
		Assert.NotNull(innerKernel);
		Assert.Equal(ItemLocationKind.Contained, innerKernel!.Value.Location.Kind);
		Assert.Equal(500UL, innerKernel.Value.Location.ParentItemId);
	}

	[Fact]
	public void Guest_MovesNestedRemoteItemToSlot_RaisesOwnerApplyWithRecursiveSource()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var inner = new CharacterItemMsg
		{
			InstanceId = 501,
			ItemId = "innerbox",
			SlotIndex = 0,
			Contents = [Item(42, "waterbottle")],
		};
		var backpack = new CharacterItemMsg
		{
			InstanceId = 500,
			ItemId = "backpack",
			SlotIndex = 0,
			Contents = [inner],
		};
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true, backpack));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		RemoteInventoryApplyMsg? apply = null;
		host.Services.GetRequiredService<IPlayerInteractionControl>().RemoteInventoryApplyReceived += m => apply = m;

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendRemoteInventoryOperation(new RemoteInventoryOperationRequestMsg
			{
				Kind = RemoteInventoryOperationKind.MoveToSlot,
				OwnerSteamId = HostId,
				ItemInstanceId = 42,
				TargetSlotIndex = 1,
			});

		Assert.NotNull(apply);
		Assert.Equal(RemoteInventoryOperationKind.MoveToSlot, apply!.Kind);
		Assert.Equal(HostId, apply.OwnerSteamId);
		Assert.Equal(42UL, apply.ItemInstanceId);
		Assert.Equal(1, apply.TargetSlotIndex);
	}

	[Fact]
	public void Guest_PoursRemotePlayersWater_EmptiesLiquidAndSendsStateResult()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true, WaterBottle(42)));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendRemoteInventoryOperation(new RemoteInventoryOperationRequestMsg
			{
				Kind = RemoteInventoryOperationKind.Pour,
				OwnerSteamId = HostId,
				ItemInstanceId = 42,
			});

		var result = UseResult(received);
		Assert.Equal(HostId, result.UserSteamId);
		Assert.Equal(0UL, result.TargetSteamId);
		Assert.NotNull(result.ItemAfter);
		Assert.Empty(result.ItemAfter!.Liquids);

		var saved = characters.GetHostCharacterData()!.Items.Single(i => i.InstanceId == 42);
		Assert.Empty(saved.Liquids);

		var authority = host.Services.GetRequiredService<ItemKernelAuthority>();
		var kernelItem = authority.FindItem(42);
		Assert.NotNull(kernelItem);
		Assert.Empty(kernelItem!.Value.Data.Liquids);
	}
}
