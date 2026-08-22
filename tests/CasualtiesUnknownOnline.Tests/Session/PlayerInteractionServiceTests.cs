using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The cross-player "take items from another player" slice (host-authoritative):
/// the host moves one carried item between its character-data snapshots, updates
/// the guest transfer table when a guest participates, and sends an
/// authoritative body mutation to the involved peers. No GameAdapter in these
/// tests — the participant receive path is the wire surface, like ItemArbitrationFlowTests.
/// </summary>
public class PlayerInteractionServiceTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	private static CharacterItemMsg Item(ulong instanceId, string itemId = "medkit", int slot = 0) => new()
	{
		InstanceId = instanceId,
		ItemId = itemId,
		SlotIndex = slot,
		Condition = 0.75f,
		Favourited = true,
	};

	private static CharacterDataMsg Snapshot(ulong owner, bool conscious, params CharacterItemMsg[] items) => new()
	{
		OwnerSteamId = owner,
		Items = [.. items],
		Health = new CharacterHealthMsg
		{
			Alive = true,
			Conscious = conscious,
			BrainHealth = conscious ? 80f : 5f,
		},
	};

	private static CharacterDataMsg SnapshotWithLimbs(ulong owner, bool conscious, bool alive = true, params CharacterItemMsg[] items)
	{
		var data = Snapshot(owner, conscious, items);
		data.Health!.Alive = alive;
		data.Limbs =
		[
			new CharacterLimbMsg { Index = 0, SkinHealth = 50f, MuscleHealth = 50f },
			new CharacterLimbMsg { Index = 1, SkinHealth = 20f, MuscleHealth = 30f },
			new CharacterLimbMsg { Index = 2, SkinHealth = 80f, MuscleHealth = 80f },
		];
		return data;
	}

	private static void MarkInWorld(TestNode node) =>
		node.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");

	private static (TestNode Host, TestNode Guest, List<(NetMsg Msg, byte[] Frame)> Received) CreateSession()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var received = new List<(NetMsg Msg, byte[] Frame)>();
		guest.Transport.MessageReceived += (_, frame) => received.Add(((NetMsg)frame[0], frame));
		MarkInWorld(host);
		MarkInWorld(guest);
		return (host, guest, received);
	}

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

		var frame = received.Single(r => r.Msg == NetMsg.PlayerInventoryTransfer).Frame;
		var transfer = NetPacket.DecodePayload<PlayerInventoryTransferMsg>(frame);
		Assert.Equal(HostId, transfer.FromSteamId);
		Assert.Equal(GuestId, transfer.ToSteamId);
		Assert.Equal(42UL, transfer.Item!.InstanceId);
		Assert.Equal("medkit", transfer.Item.ItemId);

		Assert.DoesNotContain(characters.GetHostCharacterData()!.Items, i => i.InstanceId == 42);
		Assert.Contains(characters.GetSavedCharacter(GuestId)!.Items, i => i.InstanceId == 42);
		Assert.Contains(items.GetTransferredItems(GuestId), w => w.Item.InstanceId == 42);
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

		var frame = received.Single(r => r.Msg == NetMsg.PlayerInventoryTransfer).Frame;
		var transfer = NetPacket.DecodePayload<PlayerInventoryTransferMsg>(frame);
		Assert.Equal(GuestId, transfer.FromSteamId);
		Assert.Equal(HostId, transfer.ToSteamId);
		Assert.Equal(77UL, transfer.Item!.InstanceId);
		Assert.Equal("rifle", transfer.Item.ItemId);

		Assert.DoesNotContain(characters.GetSavedCharacter(GuestId)!.Items, i => i.InstanceId == 77);
		Assert.Contains(characters.GetHostCharacterData()!.Items, i => i.InstanceId == 77);
	}

	[Fact]
	public void Take_FromConsciousPlayer_IsRefused()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true, Item(42)));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendTakeRequest(HostId, 42);

		Assert.DoesNotContain(received, r => r.Msg == NetMsg.PlayerInventoryTransfer);
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

		Assert.DoesNotContain(received, r => r.Msg == NetMsg.PlayerInventoryTransfer);
	}

	[Fact]
	public void Take_WornItem_IsRefused()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: false, Item(42, "hat", slot: -2)));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendTakeRequest(HostId, 42);

		Assert.DoesNotContain(received, r => r.Msg == NetMsg.PlayerInventoryTransfer);
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

		Assert.DoesNotContain(received, r => r.Msg == NetMsg.PlayerInventoryTransfer);
		Assert.Contains(characters.GetHostCharacterData()!.Items, i => i.InstanceId == 42);
		Assert.DoesNotContain(characters.GetSavedCharacter(GuestId)!.Items, i => i.InstanceId == 42);
	}

	[Fact]
	public void Guest_StartsCarryingUnconsciousHost_RecordsAndBroadcastsCarryState()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: false));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendCarryStartRequest(HostId);

		var frame = received.Single(r => r.Msg == NetMsg.PlayerCarryState).Frame;
		var state = NetPacket.DecodePayload<PlayerCarryStateMsg>(frame);
		Assert.Equal(GuestId, state.CarrierSteamId);
		Assert.Equal(HostId, state.CarriedSteamId);

		var interaction = host.Services.GetRequiredService<IPlayerInteractionControl>();
		Assert.True(interaction.TryGetCarried(GuestId, out var carried));
		Assert.Equal(HostId, carried);
		Assert.True(interaction.TryGetCarrier(HostId, out var carrier));
		Assert.Equal(GuestId, carrier);

		var guestInteraction = guest.Services.GetRequiredService<IPlayerInteractionControl>();
		Assert.True(guestInteraction.TryGetCarried(GuestId, out var guestCarried));
		Assert.Equal(HostId, guestCarried);
		Assert.True(guestInteraction.TryGetCarrier(HostId, out var guestCarrier));
		Assert.Equal(GuestId, guestCarrier);
	}

	[Fact]
	public void Host_StartsCarryingUnconsciousGuest_SendsCarryStateToGuest()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: false));

		host.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendCarryStartRequest(GuestId);

		var frame = received.Single(r => r.Msg == NetMsg.PlayerCarryState).Frame;
		var state = NetPacket.DecodePayload<PlayerCarryStateMsg>(frame);
		Assert.Equal(HostId, state.CarrierSteamId);
		Assert.Equal(GuestId, state.CarriedSteamId);
	}

	[Fact]
	public void Carry_ConsciousTarget_IsRefused()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendCarryStartRequest(HostId);

		Assert.DoesNotContain(received, r => r.Msg == NetMsg.PlayerCarryState);
		Assert.False(host.Services.GetRequiredService<IPlayerInteractionControl>().TryGetCarried(GuestId, out _));
	}

	[Fact]
	public void Carry_UnableCarrier_IsRefused()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: false));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: false));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendCarryStartRequest(HostId);

		Assert.DoesNotContain(received, r => r.Msg == NetMsg.PlayerCarryState);
		Assert.False(host.Services.GetRequiredService<IPlayerInteractionControl>().TryGetCarried(GuestId, out _));
	}

	[Fact]
	public void Carry_Stop_ClearsRelationAndBroadcastsEmptyState()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: false));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		var interaction = guest.Services.GetRequiredService<IPlayerInteractionControl>();
		interaction.SendCarryStartRequest(HostId);
		interaction.SendCarryStopRequest(HostId);

		var frame = received.Last(r => r.Msg == NetMsg.PlayerCarryState).Frame;
		var state = NetPacket.DecodePayload<PlayerCarryStateMsg>(frame);
		Assert.Equal(GuestId, state.CarrierSteamId);
		Assert.Equal(0UL, state.CarriedSteamId);

		var hostInteraction = host.Services.GetRequiredService<IPlayerInteractionControl>();
		Assert.False(hostInteraction.TryGetCarried(GuestId, out _));
		Assert.False(hostInteraction.TryGetCarrier(HostId, out _));
	}

	[Fact]
	public void Carry_AlreadyParticipatingInRelation_IsRefused()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: false));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		var guestInteraction = guest.Services.GetRequiredService<IPlayerInteractionControl>();
		guestInteraction.SendCarryStartRequest(HostId);

		// The host is already the carried half; the reverse request must be
		// refused (no symmetric/mutual carry in this MVP relation model).
		var hostInteraction = host.Services.GetRequiredService<IPlayerInteractionControl>();
		hostInteraction.SendCarryStartRequest(GuestId);

		Assert.Equal(1, received.Count(r => r.Msg == NetMsg.PlayerCarryState));
		Assert.True(hostInteraction.TryGetCarried(GuestId, out var carried));
		Assert.Equal(HostId, carried);
		Assert.True(hostInteraction.TryGetCarrier(HostId, out var carrier));
		Assert.Equal(GuestId, carrier);
	}

	[Fact]
	public void Guest_HealsUnconsciousHost_ConsumesItemAndSendsResult()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var items = host.Services.GetRequiredService<IItemControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: false));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, Item(42, "bandage", slot: 0)));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendHealRequest(HostId);

		var frame = received.Single(r => r.Msg == NetMsg.PlayerHealResult).Frame;
		var result = NetPacket.DecodePayload<PlayerHealResultMsg>(frame);
		Assert.Equal(GuestId, result.HealerSteamId);
		Assert.Equal(HostId, result.TargetSteamId);
		Assert.Equal(42UL, result.ItemInstanceId);
		Assert.True(result.ItemDestroyed);
		Assert.Equal(1, result.HealedLimbIndex);

		// The host's target limb gained the bandage's skin-heal amount.
		var hostData = characters.GetHostCharacterData()!;
		var healedLimb = hostData.Limbs[result.HealedLimbIndex];
		Assert.True(healedLimb.SkinHealAmount > 0f);
		Assert.True(Math.Abs(healedLimb.SkinHealAmount - 30f) < 0.001f);
		Assert.True(Math.Abs(healedLimb.BandageSlowAmount - 45f) < 0.001f);

		// The healer's item was consumed and its transfer-table entry removed.
		Assert.DoesNotContain(characters.GetSavedCharacter(GuestId)!.Items, i => i.InstanceId == 42);
		Assert.DoesNotContain(items.GetTransferredItems(GuestId), w => w.Item.InstanceId == 42);
	}

	[Fact]
	public void Host_HealsUnconsciousGuest_SendsResultToGuest()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true, Item(77, "bandage", slot: 0)));
		characters.SaveCharacterData(GuestId, SnapshotWithLimbs(GuestId, conscious: false));

		host.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendHealRequest(GuestId, 77);

		var frame = received.Single(r => r.Msg == NetMsg.PlayerHealResult).Frame;
		var result = NetPacket.DecodePayload<PlayerHealResultMsg>(frame);
		Assert.Equal(HostId, result.HealerSteamId);
		Assert.Equal(GuestId, result.TargetSteamId);
		Assert.Equal(77UL, result.ItemInstanceId);
		Assert.Equal(1, result.HealedLimbIndex);

		// The host's own item is consumed; the guest's saved target limb healed.
		Assert.DoesNotContain(characters.GetHostCharacterData()!.Items, i => i.InstanceId == 77);
		Assert.True(characters.GetSavedCharacter(GuestId)!.Limbs[1].SkinHealAmount > 0f);
	}

	[Fact]
	public void Heal_NoHealItem_IsRefused()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: false));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, Item(42, "knife", slot: 0)));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendHealRequest(HostId);

		Assert.DoesNotContain(received, r => r.Msg == NetMsg.PlayerHealResult);
	}

	[Fact]
	public void Heal_UnableHealer_IsRefused()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: false));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: false, Item(42, "bandage", slot: 0)));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendHealRequest(HostId);

		Assert.DoesNotContain(received, r => r.Msg == NetMsg.PlayerHealResult);
		Assert.Contains(characters.GetSavedCharacter(GuestId)!.Items, i => i.InstanceId == 42);
	}

	[Fact]
	public void Heal_DeadTarget_IsRefused()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: false, alive: false));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, Item(42, "bandage", slot: 0)));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendHealRequest(HostId);

		Assert.DoesNotContain(received, r => r.Msg == NetMsg.PlayerHealResult);
	}

	[Fact]
	public void Heal_PartialCondition_PreservesItemAndUpdatesTransferTable()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var items = host.Services.GetRequiredService<IItemControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: false));
		var bandage = Item(42, "bandage", slot: 0);
		bandage.Condition = 1.5f;
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, bandage));
		items.AdoptTransferredItem(GuestId, 42, bandage);

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendHealRequest(HostId, 42);

		var frame = received.Single(r => r.Msg == NetMsg.PlayerHealResult).Frame;
		var result = NetPacket.DecodePayload<PlayerHealResultMsg>(frame);
		Assert.False(result.ItemDestroyed);
		Assert.True(Math.Abs(result.ItemConditionAfter - 0.5f) < 0.001f);

		var saved = characters.GetSavedCharacter(GuestId)!.Items.Single(i => i.InstanceId == 42);
		Assert.True(Math.Abs(saved.Condition - 0.5f) < 0.001f);
		var transferred = items.GetTransferredItems(GuestId).Single(w => w.Item.InstanceId == 42);
		Assert.True(Math.Abs(transferred.Item.Condition - 0.5f) < 0.001f);
	}
}
