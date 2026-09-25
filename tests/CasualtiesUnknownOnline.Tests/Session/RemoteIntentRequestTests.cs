using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static CasualtiesUnknownOnline.Tests.Session.PlayerInteractionTestSession;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// Behavior family: the host half of the native inventory intents. The host
/// validates the ownership fact, the operands and the destination body,
/// arbitrates the item first-writer-wins, forwards the intent to the client that
/// owns the real items, and keeps the two intents that have no single native
/// call behind them (<c>TransferToBody</c>, <c>ApplyToLimb</c>) on their landed
/// host-authoritative paths. It never models inventory contents.
/// </summary>
[Trait("Category", "Integration")]
public class RemoteIntentRequestTests
{
	[Fact]
	public void HostOwner_IntentRequest_RaisesTheIntentOnTheHost()
	{
		var (host, guest, _) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true, Item(42)));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		RemoteInventoryIntentMsg? intent = null;
		host.Services.GetRequiredService<IPlayerInteractionControl>().RemoteInventoryIntentReceived += m => intent = m;

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendRemoteInventoryIntent(new RemoteInventoryIntentMsg
			{
				Kind = RemoteInventoryIntentKind.DropItem,
				OwnerSteamId = HostId,
				ItemInstanceId = 42,
			});

		Assert.NotNull(intent);
		Assert.Equal(RemoteInventoryIntentKind.DropItem, intent!.Kind);
		Assert.Equal(42UL, intent.ItemInstanceId);
		Assert.Contains(characters.GetHostCharacterData()!.Items, i => i.InstanceId == 42);
	}

	[Fact]
	public void GuestOwner_IntentRequest_IsForwardedToTheOwner()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, Item(42)));

		host.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendRemoteInventoryIntent(new RemoteInventoryIntentMsg
			{
				Kind = RemoteInventoryIntentKind.TakeOutOfContainer,
				OwnerSteamId = GuestId,
				ItemInstanceId = 42,
			});

		var frame = received.Single(r => r.Msg == NetMsg.RemoteInventoryIntent).Frame;
		var forwarded = NetPacket.DecodePayload<RemoteInventoryIntentMsg>(frame);
		Assert.Equal(RemoteInventoryIntentKind.TakeOutOfContainer, forwarded.Kind);
		Assert.Equal(GuestId, forwarded.OwnerSteamId);
		Assert.Equal(42UL, forwarded.ItemInstanceId);
	}

	[Fact]
	public void IntentRequest_ForAnItemTheOwnerDoesNotCarry_IsRefused()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true, Item(42)));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		RemoteInventoryIntentMsg? intent = null;
		host.Services.GetRequiredService<IPlayerInteractionControl>().RemoteInventoryIntentReceived += m => intent = m;

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendRemoteInventoryIntent(new RemoteInventoryIntentMsg
			{
				Kind = RemoteInventoryIntentKind.DropItem,
				OwnerSteamId = HostId,
				ItemInstanceId = 999,
			});

		Assert.Null(intent);
		Assert.DoesNotContain(received, r => r.Msg == NetMsg.RemoteInventoryIntent);
	}

	[Fact]
	public void SlotIntent_OutsideTheOwnersInventory_IsRefused()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true, Item(42)));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		RemoteInventoryIntentMsg? intent = null;
		host.Services.GetRequiredService<IPlayerInteractionControl>().RemoteInventoryIntentReceived += m => intent = m;

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendRemoteInventoryIntent(new RemoteInventoryIntentMsg
			{
				Kind = RemoteInventoryIntentKind.PickUpToSlot,
				OwnerSteamId = HostId,
				ItemInstanceId = 42,
				TargetSlotIndex = 9,
			});

		Assert.Null(intent);
		Assert.DoesNotContain(received, r => r.Msg == NetMsg.RemoteInventoryIntent);
	}

	[Fact]
	public void TransferToBody_ToAThirdBody_IsRefused()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true, Item(42)));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendRemoteInventoryIntent(new RemoteInventoryIntentMsg
			{
				Kind = RemoteInventoryIntentKind.TransferToBody,
				OwnerSteamId = HostId,
				ItemInstanceId = 42,
				TargetBodySteamId = 4242,
				TargetSlotIndex = 0,
			});

		Assert.DoesNotContain(received, r => r.Msg == NetMsg.RemoteInventoryIntent);
		Assert.Contains(characters.GetHostCharacterData()!.Items, i => i.InstanceId == 42);
	}

	[Fact]
	public void Guest_TransferToBody_MovesConsciousHostItemToTheNamedSlot()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true, Item(42)));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendRemoteInventoryIntent(new RemoteInventoryIntentMsg
			{
				Kind = RemoteInventoryIntentKind.TransferToBody,
				OwnerSteamId = HostId,
				ItemInstanceId = 42,
				TargetBodySteamId = GuestId,
				TargetSlotIndex = 1,
			});

		var transfer = TransferResult(received);
		Assert.Equal(HostId, transfer.FromSteamId);
		Assert.Equal(GuestId, transfer.ToSteamId);
		Assert.Equal(42UL, transfer.Item!.InstanceId);
		Assert.Equal(1, transfer.Item.SlotIndex);
		Assert.DoesNotContain(characters.GetHostCharacterData()!.Items, i => i.InstanceId == 42);
		Assert.Contains(characters.GetSavedCharacter(GuestId)!.Items, i => i.InstanceId == 42);
	}

	[Fact]
	public void Host_TransferToBody_MovesConsciousGuestItemToSelf()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var items = host.Services.GetRequiredService<IItemControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		var water = WaterBottle(42);
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, water));
		items.AdoptTransferredItem(GuestId, 42, water);

		host.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendRemoteInventoryIntent(new RemoteInventoryIntentMsg
			{
				Kind = RemoteInventoryIntentKind.TransferToBody,
				OwnerSteamId = GuestId,
				ItemInstanceId = 42,
				TargetBodySteamId = HostId,
				TargetSlotIndex = -1,
			});

		var transfer = TransferResult(received);
		Assert.Equal(GuestId, transfer.FromSteamId);
		Assert.Equal(HostId, transfer.ToSteamId);
		Assert.Equal(42UL, transfer.Item!.InstanceId);
		Assert.DoesNotContain(characters.GetSavedCharacter(GuestId)!.Items, i => i.InstanceId == 42);
		Assert.Contains(characters.GetHostCharacterData()!.Items, i => i.InstanceId == 42);
	}

	[Fact]
	public void ContainerMove_IsForwardedToTheOwner()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true, Item(42), Item(77, "backpack")));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		RemoteInventoryIntentMsg? intent = null;
		host.Services.GetRequiredService<IPlayerInteractionControl>().RemoteInventoryIntentReceived += m => intent = m;

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendRemoteInventoryIntent(new RemoteInventoryIntentMsg
			{
				Kind = RemoteInventoryIntentKind.MoveIntoContainer,
				OwnerSteamId = HostId,
				ItemInstanceId = 42,
				TargetContainerInstanceId = 77,
			});

		Assert.NotNull(intent);
		Assert.Equal(RemoteInventoryIntentKind.MoveIntoContainer, intent!.Kind);
		Assert.Equal(77UL, intent.TargetContainerInstanceId);
	}

	[Fact]
	public void ContainerMove_ToTheItemItself_IsRefused()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true, Item(42)));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		RemoteInventoryIntentMsg? intent = null;
		host.Services.GetRequiredService<IPlayerInteractionControl>().RemoteInventoryIntentReceived += m => intent = m;

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendRemoteInventoryIntent(new RemoteInventoryIntentMsg
			{
				Kind = RemoteInventoryIntentKind.MoveIntoContainer,
				OwnerSteamId = HostId,
				ItemInstanceId = 42,
				TargetContainerInstanceId = 42,
			});

		Assert.Null(intent);
		Assert.DoesNotContain(received, r => r.Msg == NetMsg.RemoteInventoryIntent);
	}

	[Fact]
	public void SlotSwap_IntoAHandSlot_IsAdmitted()
	{
		var (host, guest, _) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var hostData = Snapshot(HostId, conscious: true, Item(42), Item(43, "knife", slot: 1));
		hostData.SlotCount = 3;
		characters.SaveHostCharacterData(hostData);
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		RemoteInventoryIntentMsg? intent = null;
		host.Services.GetRequiredService<IPlayerInteractionControl>().RemoteInventoryIntentReceived += m => intent = m;

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendRemoteInventoryIntent(new RemoteInventoryIntentMsg
			{
				Kind = RemoteInventoryIntentKind.SwapSlots,
				OwnerSteamId = HostId,
				ItemInstanceId = 42,
				TargetSlotIndex = 0,
			});

		Assert.NotNull(intent);
		Assert.Equal(RemoteInventoryIntentKind.SwapSlots, intent!.Kind);
		Assert.Equal(0, intent.TargetSlotIndex);
	}

	[Fact]
	public void WornDrop_ForAnItemInsideAContainer_IsForwardedToTheOwner()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, Item(42)));

		host.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendRemoteInventoryIntent(new RemoteInventoryIntentMsg
			{
				Kind = RemoteInventoryIntentKind.DropWearable,
				OwnerSteamId = GuestId,
				ItemInstanceId = 42,
			});

		var frame = received.Single(r => r.Msg == NetMsg.RemoteInventoryIntent).Frame;
		Assert.Equal(RemoteInventoryIntentKind.DropWearable, NetPacket.DecodePayload<RemoteInventoryIntentMsg>(frame).Kind);
	}

	[Fact]
	public void TakeOutOfContainer_ForAnItemInsideAContainer_IsForwardedToTheOwner()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, Item(42)));

		host.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendRemoteInventoryIntent(new RemoteInventoryIntentMsg
			{
				Kind = RemoteInventoryIntentKind.TakeOutOfContainer,
				OwnerSteamId = GuestId,
				ItemInstanceId = 42,
			});

		var frame = received.Single(r => r.Msg == NetMsg.RemoteInventoryIntent).Frame;
		Assert.Equal(RemoteInventoryIntentKind.TakeOutOfContainer, NetPacket.DecodePayload<RemoteInventoryIntentMsg>(frame).Kind);
	}

	[Fact]
	public void ContainerTakeOutAndBack_ForAGuestOwner_LeavesTheHostsCopyOfTheInventoryAlone()
	{
		// The reported vanish: an item taken out of the trash bag disappeared a short
		// while later, because the host had edited ITS OWN copy of the owner's
		// character data (the deleted mirror-edit path) and the owner's next report
		// overwrote that edit. The host now only validates, arbitrates and forwards, so
		// its copy of the owner's inventory must not move at all — there is nothing
		// left that the next authoritative report could undo.
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, Item(42), Item(78, "trashbag")));

		var interaction = host.Services.GetRequiredService<IPlayerInteractionControl>();

		interaction.SendRemoteInventoryIntent(new RemoteInventoryIntentMsg
		{
			Kind = RemoteInventoryIntentKind.MoveIntoContainer,
			OwnerSteamId = GuestId,
			ItemInstanceId = 42,
			TargetContainerInstanceId = 78,
		});
		interaction.SendRemoteInventoryIntent(new RemoteInventoryIntentMsg
		{
			Kind = RemoteInventoryIntentKind.TakeOutOfContainer,
			OwnerSteamId = GuestId,
			ItemInstanceId = 42,
		});

		var forwarded = received.Where(r => r.Msg == NetMsg.RemoteInventoryIntent).ToList();
		Assert.Equal(2, forwarded.Count);
		Assert.Equal(RemoteInventoryIntentKind.MoveIntoContainer, NetPacket.DecodePayload<RemoteInventoryIntentMsg>(forwarded[0].Frame).Kind);
		Assert.Equal(RemoteInventoryIntentKind.TakeOutOfContainer, NetPacket.DecodePayload<RemoteInventoryIntentMsg>(forwarded[1].Frame).Kind);

		var owner = characters.GetSavedCharacter(GuestId)!;
		Assert.Contains(owner.Items, i => i.InstanceId == 42);
		Assert.Contains(owner.Items, i => i.InstanceId == 78);
	}

	[Fact]
	public void ContainerChildBatch_IsForwardedToTheOwner()
	{
		// R5's batch names the dragged container and the target container only: the
		// host has nothing else to validate, because which children fit is decided by
		// the owner's own native guard.
		var (host, guest, _) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true, Item(77, "backpack"), Item(78, "trashbag")));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		RemoteInventoryIntentMsg? intent = null;
		host.Services.GetRequiredService<IPlayerInteractionControl>().RemoteInventoryIntentReceived += m => intent = m;

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendRemoteInventoryIntent(new RemoteInventoryIntentMsg
			{
				Kind = RemoteInventoryIntentKind.MoveContainerChildren,
				OwnerSteamId = HostId,
				ItemInstanceId = 77,
				TargetContainerInstanceId = 78,
			});

		Assert.NotNull(intent);
		Assert.Equal(RemoteInventoryIntentKind.MoveContainerChildren, intent!.Kind);
		Assert.Equal(77UL, intent.ItemInstanceId);
		Assert.Equal(78UL, intent.TargetContainerInstanceId);
	}

	[Fact]
	public void ContainerChildBatch_IntoItself_IsRefused()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true, Item(77, "backpack")));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		RemoteInventoryIntentMsg? intent = null;
		host.Services.GetRequiredService<IPlayerInteractionControl>().RemoteInventoryIntentReceived += m => intent = m;

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendRemoteInventoryIntent(new RemoteInventoryIntentMsg
			{
				Kind = RemoteInventoryIntentKind.MoveContainerChildren,
				OwnerSteamId = HostId,
				ItemInstanceId = 77,
				TargetContainerInstanceId = 77,
			});

		Assert.Null(intent);
		Assert.DoesNotContain(received, r => r.Msg == NetMsg.RemoteInventoryIntent);
	}

	[Fact]
	public void DrainTick_IsForwardedToTheOwnerWithItsAmount()
	{
		var (host, guest, _) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true, WaterBottle(42)));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		RemoteInventoryIntentMsg? intent = null;
		host.Services.GetRequiredService<IPlayerInteractionControl>().RemoteInventoryIntentReceived += m => intent = m;

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendRemoteInventoryIntent(new RemoteInventoryIntentMsg
			{
				Kind = RemoteInventoryIntentKind.Drain,
				OwnerSteamId = HostId,
				ItemInstanceId = 42,
				Amount = 0.25f,
			});

		Assert.NotNull(intent);
		Assert.Equal(RemoteInventoryIntentKind.Drain, intent!.Kind);
		Assert.Equal(0.25f, intent.Amount);
	}

	[Theory]
	[InlineData(float.NaN)]
	[InlineData(float.PositiveInfinity)]
	[InlineData(-1f)]
	public void DrainTick_WithAnUnusableAmount_IsRefused(float amount)
	{
		// A non-finite amount would poison the owner's liquid stacks through the
		// native call and a negative one would add liquid; neither is a value the
		// native tick can produce, so the host refuses the intent outright.
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true, WaterBottle(42)));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		RemoteInventoryIntentMsg? intent = null;
		host.Services.GetRequiredService<IPlayerInteractionControl>().RemoteInventoryIntentReceived += m => intent = m;

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendRemoteInventoryIntent(new RemoteInventoryIntentMsg
			{
				Kind = RemoteInventoryIntentKind.Drain,
				OwnerSteamId = HostId,
				ItemInstanceId = 42,
				Amount = amount,
			});

		Assert.Null(intent);
		Assert.DoesNotContain(received, r => r.Msg == NetMsg.RemoteInventoryIntent);
	}
}
