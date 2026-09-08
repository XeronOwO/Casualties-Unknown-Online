using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static CasualtiesUnknownOnline.Tests.Session.PlayerInteractionTestSession;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Behavior family: wearing an item onto another player (slot adoption and refusals).
/// Split from PlayerInteractionServiceTests so xUnit v2 (one collection per class,
/// serial inside a class) does not serialize the whole player-interaction surface.
/// </summary>
[Trait("Category", "Integration")]
public class WearTests
{
	[Fact]
	public void Guest_WearsHelmetOnHost_MovesItemAndSendsWornResult()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var items = host.Services.GetRequiredService<IItemControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: true));
		var helmet = Item(42, "bikehelmet", slot: 0);
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, helmet));
		items.AdoptTransferredItem(GuestId, 42, helmet);

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendUseRequest(HostId, 42);

		var result = UseResult(received);
		Assert.Equal(GuestId, result.UserSteamId);
		Assert.Equal(HostId, result.TargetSteamId);
		Assert.True(result.ItemDestroyed);
		Assert.Null(result.ItemAfter);
		Assert.NotNull(result.WornItem);
		Assert.Equal("bikehelmet", result.WornItem!.ItemId);
		Assert.Equal(-2, result.WornItem.SlotIndex);

		var hostData = characters.GetHostCharacterData()!;
		Assert.Contains(hostData.Items, i => i.InstanceId == 42 && i.SlotIndex == -2);
		Assert.DoesNotContain(characters.GetSavedCharacter(GuestId)!.Items, i => i.InstanceId == 42);
		Assert.Empty(items.GetTransferredItems(GuestId));

		var authority = host.Services.GetRequiredService<ItemKernelAuthority>();
		var kernelItem = authority.FindItem(42);
		Assert.NotNull(kernelItem);
		Assert.Equal(ItemLocationKind.Carried, kernelItem!.Value.Location.Kind);
		Assert.Equal(HostId, kernelItem.Value.Location.Owner.Value);

		// The guest's replay kernel also sees the worn item move to the host.
		var guestAuthority = guest.Services.GetRequiredService<ItemKernelAuthority>();
		var guestKernelItem = guestAuthority.FindItem(42);
		Assert.NotNull(guestKernelItem);
		Assert.Equal(ItemLocationKind.Carried, guestKernelItem!.Value.Location.Kind);
		Assert.Equal(HostId, guestKernelItem.Value.Location.Owner.Value);
	}

	[Fact]
	public void Host_WearsHelmetOnGuest_MovesItemAndAdoptsForGuest()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var items = host.Services.GetRequiredService<IItemControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true, Item(42, "bikehelmet", slot: 0)));
		characters.SaveCharacterData(GuestId, SnapshotWithLimbs(GuestId, conscious: true));

		host.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendUseRequest(GuestId, 42);

		var result = UseResult(received);
		Assert.Equal(HostId, result.UserSteamId);
		Assert.Equal(GuestId, result.TargetSteamId);
		Assert.True(result.ItemDestroyed);
		Assert.NotNull(result.WornItem);
		Assert.Equal(-2, result.WornItem!.SlotIndex);

		Assert.DoesNotContain(characters.GetHostCharacterData()!.Items, i => i.InstanceId == 42);
		var guestData = characters.GetSavedCharacter(GuestId)!;
		Assert.Contains(guestData.Items, i => i.InstanceId == 42 && i.SlotIndex == -2);
		Assert.Contains(items.GetTransferredItems(GuestId), w => w.Item.InstanceId == 42);
	}

	[Fact]
	public void Wear_TargetAlreadyUsesSameWearSlot_IsRefused()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var items = host.Services.GetRequiredService<IItemControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: true, alive: true, Item(99, "holidayhat", slot: -2)));
		var helmet = Item(42, "bikehelmet", slot: 0);
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, helmet));
		items.AdoptTransferredItem(GuestId, 42, helmet);

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendUseRequest(HostId, 42);

		Assert.DoesNotContain(KernelEvents(received), e => e.Kind == WireEventKind.PlayerItemUseResult);
		Assert.Contains(characters.GetSavedCharacter(GuestId)!.Items, i => i.InstanceId == 42);
		Assert.Contains(items.GetTransferredItems(GuestId), w => w.Item.InstanceId == 42);
	}

	[Fact]
	public void Wear_TargetLimbDismembered_IsRefused()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var items = host.Services.GetRequiredService<IItemControl>();
		var hostSnapshot = SnapshotWithLimbs(HostId, conscious: true);
		hostSnapshot.Limbs[0].Dismembered = true;
		characters.SaveHostCharacterData(hostSnapshot);
		var helmet = Item(42, "bikehelmet", slot: 0);
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, helmet));
		items.AdoptTransferredItem(GuestId, 42, helmet);

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendUseRequest(HostId, 42);

		Assert.DoesNotContain(KernelEvents(received), e => e.Kind == WireEventKind.PlayerItemUseResult);
		Assert.Contains(characters.GetSavedCharacter(GuestId)!.Items, i => i.InstanceId == 42);
		Assert.Contains(items.GetTransferredItems(GuestId), w => w.Item.InstanceId == 42);
	}
}
