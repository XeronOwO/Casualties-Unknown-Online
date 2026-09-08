using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Xunit;
using static CasualtiesUnknownOnline.Tests.Session.PlayerInteractionTestSession;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Behavior family: taking a carried item from another player (host-authoritative record move + transfer event + body mutation).
/// Split from PlayerInteractionServiceTests so xUnit v2 (one collection per class,
/// serial inside a class) does not serialize the whole player-interaction surface.
/// </summary>
[Trait("Category", "Integration")]
public class TakeTests
{
	[Fact]
	public void Guest_TakesItemFromUnconsciousHost_MovesRecordAndSendsTransfer()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var items = host.Services.GetRequiredService<IItemControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: false, Item(42)));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendTakeRequest(HostId, 42);

		var transfer = TransferResult(received);
		Assert.Equal(HostId, transfer.FromSteamId);
		Assert.Equal(GuestId, transfer.ToSteamId);
		Assert.Equal(42UL, transfer.Item!.InstanceId);
		Assert.Equal("medkit", transfer.Item.ItemId);

		Assert.DoesNotContain(characters.GetHostCharacterData()!.Items, i => i.InstanceId == 42);
		Assert.Contains(characters.GetSavedCharacter(GuestId)!.Items, i => i.InstanceId == 42);
		Assert.Contains(items.GetTransferredItems(GuestId), w => w.Item.InstanceId == 42);
	}

	[Fact]
	public void Guest_TakeResult_ProjectsTransferEventOnBothParticipants()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: false, Item(42)));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		PlayerInventoryTransferMsg? hostTransfer = null;
		PlayerInventoryTransferMsg? guestTransfer = null;
		host.Services.GetRequiredService<IPlayerInteractionControl>().TransferReceived += m => hostTransfer = m;
		guest.Services.GetRequiredService<IPlayerInteractionControl>().TransferReceived += m => guestTransfer = m;

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendTakeRequest(HostId, 42);

		Assert.NotNull(hostTransfer);
		Assert.Equal(HostId, hostTransfer!.FromSteamId);
		Assert.Equal(GuestId, hostTransfer.ToSteamId);
		Assert.NotNull(guestTransfer);
		Assert.Equal(HostId, guestTransfer!.FromSteamId);
		Assert.Equal(GuestId, guestTransfer.ToSteamId);
	}

	[Fact]
	public void Host_TakesItemFromUnconsciousGuest_SendsTransferToGuest()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: false, Item(77, "rifle", slot: 1)));

		host.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendTakeRequest(GuestId, 77);

		var transfer = TransferResult(received);
		Assert.Equal(GuestId, transfer.FromSteamId);
		Assert.Equal(HostId, transfer.ToSteamId);
		Assert.Equal(77UL, transfer.Item!.InstanceId);
		Assert.Equal("rifle", transfer.Item.ItemId);

		Assert.DoesNotContain(characters.GetSavedCharacter(GuestId)!.Items, i => i.InstanceId == 77);
		Assert.Contains(characters.GetHostCharacterData()!.Items, i => i.InstanceId == 77);

		var authority = host.Services.GetRequiredService<ItemKernelAuthority>();
		var kernelItem = authority.FindItem(77);
		Assert.NotNull(kernelItem);
		Assert.Equal(ItemLocationKind.Carried, kernelItem!.Value.Location.Kind);
		Assert.Equal(HostId, kernelItem.Value.Location.Owner.Value);

		// The guest's replay authority receives the same item batch through
		// KernelEnvelope, so the durable ownership fact is not host-only.
		var guestAuthority = guest.Services.GetRequiredService<ItemKernelAuthority>();
		var guestKernelItem = guestAuthority.FindItem(77);
		Assert.NotNull(guestKernelItem);
		Assert.Equal(ItemLocationKind.Carried, guestKernelItem!.Value.Location.Kind);
		Assert.Equal(HostId, guestKernelItem.Value.Location.Owner.Value);
	}

	[Fact]
	public void Guest_TakesNestedItemFromUnconsciousHostContainer_MovesRecordAndSendsTransfer()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var items = host.Services.GetRequiredService<IItemControl>();
		var backpack = new CharacterItemMsg
		{
			InstanceId = 500,
			ItemId = "backpack",
			SlotIndex = 0,
			Contents =
			[
				new CharacterItemMsg { InstanceId = 42, ItemId = "medkit", SlotIndex = 0 },
			],
		};
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: false, backpack));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendTakeRequest(HostId, 42);

		var transfer = TransferResult(received);
		Assert.Equal(HostId, transfer.FromSteamId);
		Assert.Equal(GuestId, transfer.ToSteamId);
		Assert.Equal(42UL, transfer.Item!.InstanceId);
		Assert.Equal("medkit", transfer.Item.ItemId);

		var hostData = characters.GetHostCharacterData()!;
		var remainingBackpack = Assert.Single(hostData.Items);
		Assert.Equal(500UL, remainingBackpack.InstanceId);
		Assert.Empty(remainingBackpack.Contents);

		Assert.Contains(characters.GetSavedCharacter(GuestId)!.Items, i => i.InstanceId == 42);
		Assert.Contains(items.GetTransferredItems(GuestId), w => w.Item.InstanceId == 42);
	}

	[Fact]
	public void Guest_TakesItemFromNestedContainerInsideUnconsciousHost_RemovesOnlyFromDeepestParent()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var outer = new CharacterItemMsg
		{
			InstanceId = 40,
			ItemId = "outerbackpack",
			SlotIndex = 0,
			Contents =
			[
				new CharacterItemMsg
				{
					InstanceId = 41,
					ItemId = "innerbox",
					SlotIndex = 0,
					Contents =
					[
						new CharacterItemMsg { InstanceId = 42, ItemId = "medkit", SlotIndex = 0 },
					],
				},
			],
		};
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: false, outer));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendTakeRequest(HostId, 42);

		var transfer = TransferResult(received);
		Assert.Equal(42UL, transfer.Item!.InstanceId);

		var hostData = characters.GetHostCharacterData()!;
		var outerAfter = Assert.Single(hostData.Items);
		var innerAfter = Assert.Single(outerAfter.Contents);
		Assert.Equal(41UL, innerAfter.InstanceId);
		Assert.Empty(innerAfter.Contents);

		Assert.Contains(characters.GetSavedCharacter(GuestId)!.Items, i => i.InstanceId == 42);
	}

	[Fact]
	public void Take_FromConsciousPlayer_IsRefused()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true, Item(42)));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendTakeRequest(HostId, 42);

		Assert.DoesNotContain(KernelEvents(received), e => e.Kind == WireEventKind.PlayerInventoryTransfer);
		Assert.Contains(characters.GetHostCharacterData()!.Items, i => i.InstanceId == 42);
	}

	[Fact]
	public void Take_SourceWithoutHealthSnapshot_IsRefused()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(new CharacterDataMsg
		{
			OwnerSteamId = HostId,
			Items = [Item(42)],
		});
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendTakeRequest(HostId, 42);

		Assert.DoesNotContain(KernelEvents(received), e => e.Kind == WireEventKind.PlayerInventoryTransfer);
		Assert.Contains(characters.GetHostCharacterData()!.Items, i => i.InstanceId == 42);
	}

	[Fact]
	public void Take_NestedItemFromConsciousPlayer_IsRefused()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true, new CharacterItemMsg
		{
			InstanceId = 500,
			ItemId = "backpack",
			SlotIndex = 0,
			Contents =
			[
				new CharacterItemMsg { InstanceId = 42, ItemId = "medkit", SlotIndex = 0 },
			],
		}));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendTakeRequest(HostId, 42);

		Assert.DoesNotContain(KernelEvents(received), e => e.Kind == WireEventKind.PlayerInventoryTransfer);
		var hostData = characters.GetHostCharacterData()!;
		var backpack = Assert.Single(hostData.Items);
		Assert.Contains(backpack.Contents, i => i.InstanceId == 42);
	}

	[Fact]
	public void Take_RemoteInventoryTakeDisabled_RefusesEvenUnconsciousTarget()
	{
		var (host, guest, received) = CreateSession(s => s.Replace(
			ServiceDescriptor.Singleton<IOptionsMonitor<HostRulesOptions>>(
				new MutableOptionsMonitor<HostRulesOptions>(new HostRulesOptions { AllowRemoteInventoryTake = false }))));
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: false, Item(42)));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendTakeRequest(HostId, 42);

		Assert.DoesNotContain(KernelEvents(received), e => e.Kind == WireEventKind.PlayerInventoryTransfer);
		Assert.Contains(characters.GetHostCharacterData()!.Items, i => i.InstanceId == 42);
	}

	[Fact]
	public void Take_UnknownItem_IsRefused()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: false, Item(42)));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendTakeRequest(HostId, 999);

		Assert.DoesNotContain(KernelEvents(received), e => e.Kind == WireEventKind.PlayerInventoryTransfer);
	}

	[Fact]
	public void Take_WornItem_IsRefused()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: false, Item(42, "hat", slot: -2)));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendTakeRequest(HostId, 42);

		Assert.DoesNotContain(KernelEvents(received), e => e.Kind == WireEventKind.PlayerInventoryTransfer);
		Assert.Contains(characters.GetHostCharacterData()!.Items, i => i.InstanceId == 42);
	}

	[Fact]
	public void Take_TargetWithNoEmptySlot_IsRefused()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: false, Item(42)));

		var guestData = Snapshot(GuestId, conscious: true,
			Item(101, "water", slot: 0),
			Item(102, "food", slot: 1),
			Item(103, "knife", slot: 2));
		guestData.SlotCount = 3;
		characters.SaveCharacterData(GuestId, guestData);

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendTakeRequest(HostId, 42);

		Assert.DoesNotContain(KernelEvents(received), e => e.Kind == WireEventKind.PlayerInventoryTransfer);
		Assert.Contains(characters.GetHostCharacterData()!.Items, i => i.InstanceId == 42);
		Assert.DoesNotContain(characters.GetSavedCharacter(GuestId)!.Items, i => i.InstanceId == 42);
	}
}
