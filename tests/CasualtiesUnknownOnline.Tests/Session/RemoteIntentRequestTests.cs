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
	public void Guest_GiveToBody_MovesOwnItemToTheHostsNamedSlot()
	{
		// The mirror of TransferToBody: here the item belongs to the REQUESTER, who is giving it away, and the
		// destination body is the other player's. One host core runs both directions with the two ends swapped, so
		// the two halves of a hand-over cannot drift apart about custody.
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var items = host.Services.GetRequiredService<IItemControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		var water = WaterBottle(42);
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, water));
		items.AdoptTransferredItem(GuestId, 42, water);

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendRemoteInventoryIntent(new RemoteInventoryIntentMsg
			{
				Kind = RemoteInventoryIntentKind.GiveToBody,
				OwnerSteamId = GuestId,
				ItemInstanceId = 42,
				TargetBodySteamId = HostId,
				TargetSlotIndex = 2,
			});

		var transfer = TransferResult(received);
		Assert.Equal(GuestId, transfer.FromSteamId);
		Assert.Equal(HostId, transfer.ToSteamId);
		Assert.Equal(42UL, transfer.Item!.InstanceId);
		Assert.Equal(2, transfer.Item.SlotIndex);
		Assert.DoesNotContain(characters.GetSavedCharacter(GuestId)!.Items, i => i.InstanceId == 42);
		Assert.Contains(characters.GetHostCharacterData()!.Items, i => i.InstanceId == 42);
	}

	[Fact]
	public void Guest_GiveToBody_IntoTheHostsCarriedContainer_PutsTheItemInsideIt()
	{
		// The user's ruling covers BOTH targets of the displayed backpack: a ring slot puts the item on the body, and
		// an ITEM shown in it puts the item INTO that container. What this pins is the TWO things that make that
		// happen: the host's snapshot nests the item under the container, and the outgoing transfer NAMES the
		// container — the receiving client's placement hangs off TargetParentItemId, so a transfer without it would
		// leave the item beside the bag while this test still saw a nested snapshot.
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var items = host.Services.GetRequiredService<IItemControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true, Item(900, "backpack", slot: 3)));
		var water = WaterBottle(42);
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, water));
		items.AdoptTransferredItem(GuestId, 42, water);

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendRemoteInventoryIntent(new RemoteInventoryIntentMsg
			{
				Kind = RemoteInventoryIntentKind.GiveToBody,
				OwnerSteamId = GuestId,
				ItemInstanceId = 42,
				TargetBodySteamId = HostId,
				TargetSlotIndex = -1,
				TargetContainerInstanceId = 900,
			});

		var transfer = TransferResult(received);
		Assert.Equal(900UL, transfer.TargetParentItemId);
		Assert.Equal(3, transfer.Item!.SlotIndex); // the snapshot convention: a container child carries its PARENT's slot

		var hostData = characters.GetHostCharacterData()!;
		Assert.DoesNotContain(hostData.Items, i => i.InstanceId == 42); // inside the container, not beside it
		var container = hostData.Items.Single(i => i.InstanceId == 900);
		Assert.Contains(container.Contents, i => i.InstanceId == 42);
		Assert.DoesNotContain(characters.GetSavedCharacter(GuestId)!.Items, i => i.InstanceId == 42);
	}

	[Fact]
	public void Guest_GiveToBody_IntoAContainerTheReceiverDoesNotCarry_IsRefusedAndNothingMoves()
	{
		// The container's EXISTENCE is checked against the receiver's own snapshot, not against the sender's claim,
		// and the check lives in the host path because the operand validation cannot see that snapshot. Nothing may
		// move on either side when it fails — the giver keeps the item.
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var items = host.Services.GetRequiredService<IItemControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		var water = WaterBottle(42);
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, water));
		items.AdoptTransferredItem(GuestId, 42, water);

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendRemoteInventoryIntent(new RemoteInventoryIntentMsg
			{
				Kind = RemoteInventoryIntentKind.GiveToBody,
				OwnerSteamId = GuestId,
				ItemInstanceId = 42,
				TargetBodySteamId = HostId,
				TargetSlotIndex = -1,
				TargetContainerInstanceId = 900,
			});

		// A transfer travels as a kernel batch event rather than a NetMsg, so the refusal is read from the state it
		// would have changed: nothing left the giver and nothing arrived.
		Assert.Contains(characters.GetSavedCharacter(GuestId)!.Items, i => i.InstanceId == 42);
		Assert.Empty(characters.GetHostCharacterData()!.Items);
	}

	[Fact]
	public void GiveToBody_ToItself_IsRefusedAndNothingMoves()
	{
		// The core refuses a transfer whose two ends are the same body, and the give direction reaches that check		// the same way the take direction does — so a client cannot hand an item to itself and duplicate it.
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, Item(42)));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendRemoteInventoryIntent(new RemoteInventoryIntentMsg
			{
				Kind = RemoteInventoryIntentKind.GiveToBody,
				OwnerSteamId = GuestId,
				ItemInstanceId = 42,
				TargetBodySteamId = GuestId,
				TargetSlotIndex = 1,
			});

		Assert.Contains(characters.GetSavedCharacter(GuestId)!.Items, i => i.InstanceId == 42);
		Assert.DoesNotContain(characters.GetHostCharacterData()!.Items, i => i.InstanceId == 42);
	}

	[Fact]
	public void ARefusedIntent_IsAnsweredToTheClientThatPerformedIt()
	{
		// A gesture whose only referee is the host leaves the actor's screen identical to one that silently did
		// nothing, unless the refusal travels back. The host's own reason travels with it, so the two ends cannot
		// drift into two wordings for one refusal.
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, Item(42)));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendRemoteInventoryIntent(new RemoteInventoryIntentMsg
			{
				Kind = RemoteInventoryIntentKind.GiveToBody,
				OwnerSteamId = GuestId,
				ItemInstanceId = 42,
				TargetBodySteamId = GuestId, // refused: a hand-over to oneself
				TargetSlotIndex = 1,
			});

		var answer = received.Single(r => r.Msg == NetMsg.InventoryIntentRefused);
		var refused = NetPacket.DecodePayload<PlayerInventoryIntentRefusedMsg>(answer.Frame);
		Assert.Equal((int)RemoteInventoryIntentKind.GiveToBody, refused.Kind);
		Assert.Equal(42UL, refused.ItemInstanceId);
		Assert.False(string.IsNullOrWhiteSpace(refused.Reason));
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

	[Fact]
	public void TwoItemIntent_WithBothItemsOfTheOwner_IsForwarded()
	{
		var (host, guest, _) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true, Item(42), Item(43, slot: 1)));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		RemoteInventoryIntentMsg? intent = null;
		host.Services.GetRequiredService<IPlayerInteractionControl>().RemoteInventoryIntentReceived += m => intent = m;

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendRemoteInventoryIntent(new RemoteInventoryIntentMsg
			{
				Kind = RemoteInventoryIntentKind.CombineItems,
				OwnerSteamId = HostId,
				ItemInstanceId = 42,
				TargetItemInstanceId = 43,
			});

		Assert.NotNull(intent);
		Assert.Equal(RemoteInventoryIntentKind.CombineItems, intent!.Kind);
		Assert.Equal(42UL, intent.ItemInstanceId);
		Assert.Equal(43UL, intent.TargetItemInstanceId);
	}

	[Fact]
	public void TwoItemIntent_WithASecondItemTheOwnerDoesNotCarry_IsRefused()
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
				Kind = RemoteInventoryIntentKind.LoadBattery,
				OwnerSteamId = HostId,
				ItemInstanceId = 42,
				TargetItemInstanceId = 999,
			});

		Assert.Null(intent);
		Assert.DoesNotContain(received, r => r.Msg == NetMsg.RemoteInventoryIntent);
	}

	[Fact]
	public void TwoItemIntent_ThatNamesItsOwnItemAsTheTarget_IsRefused()
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
				Kind = RemoteInventoryIntentKind.CombineItems,
				OwnerSteamId = HostId,
				ItemInstanceId = 42,
				TargetItemInstanceId = 42,
			});

		Assert.Null(intent);
		Assert.DoesNotContain(received, r => r.Msg == NetMsg.RemoteInventoryIntent);
	}

	[Fact]
	public void TraderHandIn_WithAPosition_IsForwardedToTheOwner()
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
				Kind = RemoteInventoryIntentKind.GiveToTrader,
				OwnerSteamId = HostId,
				ItemInstanceId = 42,
				TargetTraderPosition = new NetVector2Msg(120f, -30f),
			});

		Assert.NotNull(intent);
		Assert.Equal(RemoteInventoryIntentKind.GiveToTrader, intent!.Kind);
		Assert.Equal(120f, intent.TargetTraderPosition!.X);
		Assert.Equal(-30f, intent.TargetTraderPosition.Y);
	}

	[Fact]
	public void TraderHandIn_WithoutATraderPosition_IsRefused()
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
				Kind = RemoteInventoryIntentKind.GiveToTrader,
				OwnerSteamId = HostId,
				ItemInstanceId = 42,
			});

		Assert.Null(intent);
		Assert.DoesNotContain(received, r => r.Msg == NetMsg.RemoteInventoryIntent);
	}
}
