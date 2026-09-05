using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
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

	private static (TestNode Host, TestNode Guest, List<(NetMsg Msg, byte[] Frame)> Received) CreateSession(
		Action<IServiceCollection>? extraRegistrations = null)
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId, extraRegistrations: extraRegistrations);
		var received = new List<(NetMsg Msg, byte[] Frame)>();
		guest.Transport.MessageReceived += (_, frame) => received.Add(((NetMsg)frame[0], frame));
		MarkInWorld(host);
		MarkInWorld(guest);
		return (host, guest, received);
	}

	private static List<PlayerCarryStateMsg> CarryStates(IEnumerable<(NetMsg Msg, byte[] Frame)> received) =>
		[
			.. received
				.Where(r => r.Msg == NetMsg.KernelEnvelope)
				.Select(r => NetPacket.DecodePayload<ProtocolFrame>(r.Frame))
				.Where(f => f.CommittedBatch is not null)
				.SelectMany(f => f.CommittedBatch!.Batch.Events)
				.Where(e => e.Kind is WireEventKind.PlayerCarrySet or WireEventKind.PlayerCarryCleared)
				.Select(e => new PlayerCarryStateMsg
				{
					CarrierSteamId = e.CarrierSteamId,
					CarriedSteamId = e.Kind == WireEventKind.PlayerCarrySet ? e.CarriedSteamId : 0,
				}),
		];

	private static IEnumerable<WireEvent> KernelEvents(IEnumerable<(NetMsg Msg, byte[] Frame)> received) =>
		received
			.Where(r => r.Msg == NetMsg.KernelEnvelope)
			.Select(r => NetPacket.DecodePayload<ProtocolFrame>(r.Frame))
			.Where(f => f.CommittedBatch is not null)
			.SelectMany(f => f.CommittedBatch!.Batch.Events);

	private static PlayerInventoryTransferMsg TransferResult(IEnumerable<(NetMsg Msg, byte[] Frame)> received) =>
		PlayerInteractionKernelCodec.ToTransferMessage(PlayerInteractionWireMapper.FromWireInventoryTransfer(
			KernelEvents(received).Single(e => e.Kind == WireEventKind.PlayerInventoryTransfer).PlayerInteraction!));

	private static PlayerHealResultMsg HealResult(IEnumerable<(NetMsg Msg, byte[] Frame)> received) =>
		PlayerInteractionKernelCodec.ToHealMessage(PlayerInteractionWireMapper.FromWireHealResult(
			KernelEvents(received).Single(e => e.Kind == WireEventKind.PlayerHealResult).PlayerInteraction!));

	private static PlayerItemUseResultMsg UseResult(IEnumerable<(NetMsg Msg, byte[] Frame)> received) =>
		PlayerInteractionKernelCodec.ToUseMessage(PlayerInteractionWireMapper.FromWireItemUseResult(
			KernelEvents(received).Single(e => e.Kind == WireEventKind.PlayerItemUseResult).PlayerInteraction!));

	private static void SeedHostEntities(TestNode host, ulong guestId, float guestX, float guestY = 0f, bool standing = true)
	{
		var entities = host.Services.GetRequiredService<IEntitySyncControl>();
		entities.PublishLocalState(
			new NetVector2(0f, 0f),
			new NetVector2(1f, 1f),
			NetVector2.Zero,
			isRight: true,
			standing: true,
			alive: true,
			conscious: true,
			crouching: false);
		entities.ProcessPlayerJoin(new PlayerJoinMsg
		{
			HostSteamId = HostId,
			GuestSteamId = guestId,
			HostPosition = new NetVector2Msg(0f, 0f),
			GuestPosition = new NetVector2Msg(guestX, guestY),
		});
		var guestEntity = entities.GetRemotePlayer(guestId);
		if (guestEntity is not null)
		{
			guestEntity.Position = new NetVector2(guestX, guestY);
			guestEntity.Standing = standing;
			guestEntity.Alive = true;
			guestEntity.Conscious = true;
		}
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

	[Fact]
	public void Guest_StartsCarryingUnconsciousHost_RecordsKernelCarryAndUpdatesMirrors()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: false));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendCarryStartRequest(HostId);

		var state = Assert.Single(CarryStates(received));
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
	public void Guest_StartsCarryingHost_CommitsToKernelAndClearsOnStop()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: false));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));
		var playerKernel = host.Services.GetRequiredService<PlayerKernelStatusProjection>();
		playerKernel.Sync(HostId, alive: true, conscious: true);
		playerKernel.Sync(GuestId, alive: true, conscious: true);

		var interaction = guest.Services.GetRequiredService<IPlayerInteractionControl>();
		interaction.SendCarryStartRequest(HostId);

		var authority = host.Services.GetRequiredService<ItemKernelAuthority>();
		var carrier = authority.QueryPlayers()!.Players.Single(p => p.SteamId == GuestId);
		Assert.Equal(HostId, carrier.CarrierOfSteamId);
		var carried = authority.QueryPlayers()!.Players.Single(p => p.SteamId == HostId);
		Assert.Equal(GuestId, carried.CarriedBySteamId);

		interaction.SendCarryStopRequest(HostId);

		carrier = authority.QueryPlayers()!.Players.Single(p => p.SteamId == GuestId);
		Assert.Null(carrier.CarrierOfSteamId);
		carried = authority.QueryPlayers()!.Players.Single(p => p.SteamId == HostId);
		Assert.Null(carried.CarriedBySteamId);
	}

	[Fact]
	public void Host_StartsCarryingUnconsciousGuest_RecordsKernelCarryAndUpdatesGuestMirror()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: false));

		host.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendCarryStartRequest(GuestId);

		var state = Assert.Single(CarryStates(received));
		Assert.Equal(HostId, state.CarrierSteamId);
		Assert.Equal(GuestId, state.CarriedSteamId);

		var guestInteraction = guest.Services.GetRequiredService<IPlayerInteractionControl>();
		Assert.True(guestInteraction.TryGetCarried(HostId, out var guestCarried));
		Assert.Equal(GuestId, guestCarried);
		Assert.True(guestInteraction.TryGetCarrier(GuestId, out var guestCarrier));
		Assert.Equal(HostId, guestCarrier);
	}

	[Fact]
	public void Carry_ConsciousTarget_IsRefused()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendCarryStartRequest(HostId);

		Assert.Empty(CarryStates(received));
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

		Assert.Empty(CarryStates(received));
		Assert.False(host.Services.GetRequiredService<IPlayerInteractionControl>().TryGetCarried(GuestId, out _));
	}

	[Fact]
	public void Carry_Stop_ClearsRelationAndBroadcastsKernelClear()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: false));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		var interaction = guest.Services.GetRequiredService<IPlayerInteractionControl>();
		interaction.SendCarryStartRequest(HostId);
		interaction.SendCarryStopRequest(HostId);

		var states = CarryStates(received);
		Assert.Equal(2, states.Count);
		Assert.Equal(GuestId, states[0].CarrierSteamId);
		Assert.Equal(HostId, states[0].CarriedSteamId);
		Assert.Equal(GuestId, states[1].CarrierSteamId);
		Assert.Equal(0UL, states[1].CarriedSteamId);

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

		Assert.Single(CarryStates(received));
		Assert.True(hostInteraction.TryGetCarried(GuestId, out var carried));
		Assert.Equal(HostId, carried);
		Assert.True(hostInteraction.TryGetCarrier(HostId, out var carrier));
		Assert.Equal(GuestId, carrier);
		Assert.False(hostInteraction.TryGetCarried(HostId, out _));
		Assert.False(hostInteraction.TryGetCarrier(GuestId, out _));
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

		var result = HealResult(received);
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
	public void Guest_HealsSelectedLimbOnHost_AppliesRequestedLimbNotAutoPick()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: false));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, Item(42, "bandage", slot: 0)));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendHealRequest(HostId, 42, targetLimbIndex: 0);

		var result = HealResult(received);
		Assert.Equal(0, result.HealedLimbIndex);

		var hostData = characters.GetHostCharacterData()!;
		Assert.True(Math.Abs(hostData.Limbs[0].SkinHealAmount - 30f) < 0.001f);
		Assert.True(Math.Abs(hostData.Limbs[0].BandageSlowAmount - 45f) < 0.001f);
		Assert.Equal(0f, hostData.Limbs[1].SkinHealAmount);
		Assert.Equal(0f, hostData.Limbs[1].BandageSlowAmount);
	}

	[Fact]
	public void Guest_HealResult_ProjectsHealEventOnBothParticipants()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: false));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, Item(42, "bandage", slot: 0)));

		PlayerHealResultMsg? hostHeal = null;
		PlayerHealResultMsg? guestHeal = null;
		host.Services.GetRequiredService<IPlayerInteractionControl>().HealReceived += m => hostHeal = m;
		guest.Services.GetRequiredService<IPlayerInteractionControl>().HealReceived += m => guestHeal = m;

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendHealRequest(HostId);

		Assert.NotNull(hostHeal);
		Assert.Equal(GuestId, hostHeal!.HealerSteamId);
		Assert.Equal(HostId, hostHeal.TargetSteamId);
		Assert.NotNull(guestHeal);
		Assert.Equal(GuestId, guestHeal!.HealerSteamId);
		Assert.Equal(HostId, guestHeal.TargetSteamId);
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

		var result = HealResult(received);
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

		Assert.DoesNotContain(KernelEvents(received), e => e.Kind == WireEventKind.PlayerHealResult);
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

		Assert.DoesNotContain(KernelEvents(received), e => e.Kind == WireEventKind.PlayerHealResult);
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

		Assert.DoesNotContain(KernelEvents(received), e => e.Kind == WireEventKind.PlayerHealResult);
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

		var result = HealResult(received);
		Assert.False(result.ItemDestroyed);
		Assert.True(Math.Abs(result.ItemConditionAfter - 0.5f) < 0.001f);

		var saved = characters.GetSavedCharacter(GuestId)!.Items.Single(i => i.InstanceId == 42);
		Assert.True(Math.Abs(saved.Condition - 0.5f) < 0.001f);
		var transferred = items.GetTransferredItems(GuestId).Single(w => w.Item.InstanceId == 42);
		Assert.True(Math.Abs(transferred.Item.Condition - 0.5f) < 0.001f);
	}

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

	private static CharacterItemMsg WaterBottle(ulong instanceId, float amount = 500f, float condition = 1f) => new()
	{
		InstanceId = instanceId,
		ItemId = "waterbottle",
		SlotIndex = 0,
		Condition = condition,
		Liquids = [new LiquidStackMsg { LiquidId = "water", Amount = amount }],
	};

	private static CharacterItemMsg MedicineBottle(
		ulong instanceId,
		string itemId,
		string liquidId,
		float amount = 750f,
		float condition = 1f) => new()
		{
			InstanceId = instanceId,
			ItemId = itemId,
			SlotIndex = 0,
			Condition = condition,
			Liquids = [new LiquidStackMsg { LiquidId = liquidId, Amount = amount }],
		};

	private static CharacterItemMsg TopicalBottle(
		ulong instanceId,
		string itemId,
		string liquidId,
		float amount = 100f,
		float condition = 1f) => new()
		{
			InstanceId = instanceId,
			ItemId = itemId,
			SlotIndex = 0,
			Condition = condition,
			Liquids = [new LiquidStackMsg { LiquidId = liquidId, Amount = amount }],
		};

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
	public void Guest_InjectSalineOnHost_AppliesFluidAndSendsResult()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var items = host.Services.GetRequiredService<IItemControl>();
		var hostSnapshot = SnapshotWithLimbs(HostId, conscious: true);
		hostSnapshot.Health!.BloodVolume = 100f;
		hostSnapshot.Health.Thirst = 50f;
		characters.SaveHostCharacterData(hostSnapshot);
		var saline = MedicineBottle(42, "saline", "saline");
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, saline));
		items.AdoptTransferredItem(GuestId, 42, saline);

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendUseRequest(HostId, 42);

		var result = UseResult(received);
		Assert.Equal(GuestId, result.UserSteamId);
		Assert.Equal(HostId, result.TargetSteamId);
		Assert.Equal(42UL, result.ItemInstanceId);
		Assert.False(result.ItemDestroyed);
		Assert.NotNull(result.ItemAfter);
		Assert.True(Math.Abs(result.ItemAfter!.Condition - (670f / 750f)) < 0.001f);

		var hostData = characters.GetHostCharacterData()!;
		Assert.True(Math.Abs(hostData.Health!.BloodVolume - 104.2666667f) < 0.001f);
		Assert.True(Math.Abs(hostData.Health.Thirst - 57.4666667f) < 0.001f);

		var saved = characters.GetSavedCharacter(GuestId)!.Items.Single(i => i.InstanceId == 42);
		Assert.True(Math.Abs(saved.Liquids.Single(l => l.LiquidId == "saline").Amount - 670f) < 0.001f);
		var transferred = items.GetTransferredItems(GuestId).Single(w => w.Item.InstanceId == 42);
		Assert.True(Math.Abs(transferred.Item.Liquids.Single(l => l.LiquidId == "saline").Amount - 670f) < 0.001f);
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

	[Fact]
	public void Guest_PiggybacksConsciousHost_RecordsKernelCarryAndUpdatesMirrors()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendPiggybackRequest(HostId);

		var state = Assert.Single(CarryStates(received));
		Assert.Equal(HostId, state.CarrierSteamId);
		Assert.Equal(GuestId, state.CarriedSteamId);

		var interaction = host.Services.GetRequiredService<IPlayerInteractionControl>();
		Assert.True(interaction.TryGetCarried(HostId, out var carried));
		Assert.Equal(GuestId, carried);
		Assert.True(interaction.TryGetCarrier(GuestId, out var carrier));
		Assert.Equal(HostId, carrier);
	}

	[Fact]
	public void Host_PiggybacksConsciousGuest_RecordsKernelCarryAndUpdatesGuestMirror()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		host.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendPiggybackRequest(GuestId);

		var state = Assert.Single(CarryStates(received));
		Assert.Equal(GuestId, state.CarrierSteamId);
		Assert.Equal(HostId, state.CarriedSteamId);

		var guestInteraction = guest.Services.GetRequiredService<IPlayerInteractionControl>();
		Assert.True(guestInteraction.TryGetCarried(GuestId, out var guestCarried));
		Assert.Equal(HostId, guestCarried);
		Assert.True(guestInteraction.TryGetCarrier(HostId, out var guestCarrier));
		Assert.Equal(GuestId, guestCarrier);
	}

	[Fact]
	public void Guest_InvitesHostToRideOnGuestBack_RecordsKernelCarryAndUpdatesMirrors()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendCarryOnBackRequest(HostId);

		var state = Assert.Single(CarryStates(received));
		Assert.Equal(GuestId, state.CarrierSteamId);
		Assert.Equal(HostId, state.CarriedSteamId);

		var interaction = host.Services.GetRequiredService<IPlayerInteractionControl>();
		Assert.True(interaction.TryGetCarried(GuestId, out var carried));
		Assert.Equal(HostId, carried);
		Assert.True(interaction.TryGetCarrier(HostId, out var carrier));
		Assert.Equal(GuestId, carrier);
	}

	[Fact]
	public void Host_InvitesGuestToRideOnHostBack_RecordsKernelCarryAndUpdatesGuestMirror()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		host.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendCarryOnBackRequest(GuestId);

		var state = Assert.Single(CarryStates(received));
		Assert.Equal(HostId, state.CarrierSteamId);
		Assert.Equal(GuestId, state.CarriedSteamId);

		var guestInteraction = guest.Services.GetRequiredService<IPlayerInteractionControl>();
		Assert.True(guestInteraction.TryGetCarried(HostId, out var guestCarried));
		Assert.Equal(GuestId, guestCarried);
		Assert.True(guestInteraction.TryGetCarrier(GuestId, out var guestCarrier));
		Assert.Equal(HostId, guestCarrier);
	}

	[Fact]
	public void Piggyback_DeadTarget_IsRefused()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: false, alive: false));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendPiggybackRequest(HostId);

		Assert.Empty(CarryStates(received));
		Assert.False(host.Services.GetRequiredService<IPlayerInteractionControl>().TryGetCarried(GuestId, out _));
	}

	[Fact]
	public void CarriedPlayer_CanRequestRelease()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		var guestInteraction = guest.Services.GetRequiredService<IPlayerInteractionControl>();
		guestInteraction.SendPiggybackRequest(HostId);

		// The carried player (guest) is allowed to end the ride even though it is
		// not the carrier.
		guestInteraction.SendCarryStopRequest(GuestId);

		var states = CarryStates(received);
		Assert.Equal(2, states.Count);
		Assert.Equal(HostId, states[0].CarrierSteamId);
		Assert.Equal(GuestId, states[0].CarriedSteamId);
		Assert.Equal(HostId, states[1].CarrierSteamId);
		Assert.Equal(0UL, states[1].CarriedSteamId);

		var hostInteraction = host.Services.GetRequiredService<IPlayerInteractionControl>();
		Assert.False(hostInteraction.TryGetCarried(HostId, out _));
		Assert.False(hostInteraction.TryGetCarrier(GuestId, out _));
	}

	[Fact]
	public void HostRider_CanRequestReleaseFromGuestCarrier()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		// Guest is the carrier and invites the host to ride on the guest's back.
		var guestInteraction = guest.Services.GetRequiredService<IPlayerInteractionControl>();
		guestInteraction.SendCarryOnBackRequest(HostId);

		// The host (rider) requests release through its own local row.
		var hostInteraction = host.Services.GetRequiredService<IPlayerInteractionControl>();
		Assert.True(hostInteraction.TryGetCarrier(HostId, out var carrier));
		Assert.Equal(GuestId, carrier);
		hostInteraction.SendCarryStopRequest(HostId);

		var states = CarryStates(received);
		Assert.Equal(2, states.Count);
		Assert.Equal(GuestId, states[0].CarrierSteamId);
		Assert.Equal(HostId, states[0].CarriedSteamId);
		Assert.Equal(GuestId, states[1].CarrierSteamId);
		Assert.Equal(0UL, states[1].CarriedSteamId);

		Assert.False(hostInteraction.TryGetCarrier(HostId, out _));
		Assert.False(guestInteraction.TryGetCarried(GuestId, out _));
	}

	[Fact]
	public void Guest_PushesHost_ComputesForceAndBroadcastsResult()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));
		SeedHostEntities(host, GuestId, guestX: 10f);

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendPushRequest(HostId);

		var frame = received.Single(r => r.Msg == NetMsg.PlayerPushResult).Frame;
		var result = NetPacket.DecodePayload<PlayerPushResultMsg>(frame);
		Assert.Equal(GuestId, result.PusherSteamId);
		Assert.Equal(HostId, result.TargetSteamId);
		Assert.True(result.ForceX < 0f);
		Assert.True(Math.Abs(result.ForceY) < 0.001f);
	}

	[Fact]
	public void Host_PushesGuest_SendsResultToGuest()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));
		SeedHostEntities(host, GuestId, guestX: 10f);

		host.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendPushRequest(GuestId);

		var frame = received.Single(r => r.Msg == NetMsg.PlayerPushResult).Frame;
		var result = NetPacket.DecodePayload<PlayerPushResultMsg>(frame);
		Assert.Equal(HostId, result.PusherSteamId);
		Assert.Equal(GuestId, result.TargetSteamId);
		Assert.True(result.ForceX > 0f);
		Assert.True(Math.Abs(result.ForceY) < 0.001f);
	}

	[Fact]
	public void Push_NotStandingPusher_IsRefused()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));
		SeedHostEntities(host, GuestId, guestX: 10f, standing: false);

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendPushRequest(HostId);

		Assert.DoesNotContain(received, r => r.Msg == NetMsg.PlayerPushResult);
	}

	[Fact]
	public void Push_OutOfReach_IsRefused()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));
		SeedHostEntities(host, GuestId, guestX: 20f);

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendPushRequest(HostId);

		Assert.DoesNotContain(received, r => r.Msg == NetMsg.PlayerPushResult);
	}

	[Fact]
	public void Push_ImmediateSecondRequest_IsRefusedByCooldown()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));
		SeedHostEntities(host, GuestId, guestX: 10f);

		var interaction = guest.Services.GetRequiredService<IPlayerInteractionControl>();
		interaction.SendPushRequest(HostId);
		interaction.SendPushRequest(HostId);

		Assert.Equal(1, received.Count(r => r.Msg == NetMsg.PlayerPushResult));
	}

	[Fact]
	public void Push_CarryRelation_IsRefused()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: false));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));
		SeedHostEntities(host, GuestId, guestX: 10f);

		var guestInteraction = guest.Services.GetRequiredService<IPlayerInteractionControl>();
		guestInteraction.SendCarryStartRequest(HostId);
		guestInteraction.SendPushRequest(HostId);

		Assert.DoesNotContain(received, r => r.Msg == NetMsg.PlayerPushResult);
	}

	[Fact]
	public void Guest_InjectMorphineOnHost_AppliesOpiateAndSendsResult()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var items = host.Services.GetRequiredService<IItemControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: true));
		var morphine = MedicineBottle(42, "morphine", "morphine", amount: 100f);
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, morphine));
		items.AdoptTransferredItem(GuestId, 42, morphine);

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendUseRequest(HostId, 42);

		var result = UseResult(received);
		Assert.Equal(GuestId, result.UserSteamId);
		Assert.Equal(HostId, result.TargetSteamId);
		Assert.Equal(42UL, result.ItemInstanceId);
		Assert.False(result.ItemDestroyed);
		Assert.NotNull(result.ItemAfter);
		Assert.True(Math.Abs(result.ItemAfter!.Condition - 0f) < 0.001f);

		var hostData = characters.GetHostCharacterData()!;
		Assert.True(Math.Abs(hostData.Health!.OpiateAmount - 90f) < 0.001f);

		var saved = characters.GetSavedCharacter(GuestId)!.Items.Single(i => i.InstanceId == 42);
		Assert.Empty(saved.Liquids);
		var transferred = items.GetTransferredItems(GuestId).Single(w => w.Item.InstanceId == 42);
		Assert.Empty(transferred.Item.Liquids);
	}

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

	[Fact]
	public void Guest_UsesTweezersOnHost_RemovesShrapnelAndSendsResult()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var items = host.Services.GetRequiredService<IItemControl>();
		var hostSnapshot = SnapshotWithLimbs(HostId, conscious: true);
		hostSnapshot.Limbs[1].Shrapnel = 3;
		characters.SaveHostCharacterData(hostSnapshot);
		var tweezers = Item(42, "tweezers", slot: 0);
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, tweezers));
		items.AdoptTransferredItem(GuestId, 42, tweezers);

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendUseRequest(HostId, 42);

		var result = UseResult(received);
		Assert.Equal(GuestId, result.UserSteamId);
		Assert.Equal(HostId, result.TargetSteamId);
		Assert.Equal(42UL, result.ItemInstanceId);
		Assert.False(result.ItemDestroyed);
		Assert.NotNull(result.ItemAfter);
		Assert.True(Math.Abs(result.ItemAfter!.Condition - 0.74f) < 0.001f);

		var hostData = characters.GetHostCharacterData()!;
		Assert.Equal(0, hostData.Limbs[1].Shrapnel);
		Assert.Empty(result.TimedEffects);
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
		var (host, guest, received) = CreateSession();
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

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendUseRequest(HostId, 42);

		var result = UseResult(received);
		Assert.Equal(GuestId, result.UserSteamId);
		Assert.Equal(HostId, result.TargetSteamId);
		Assert.False(result.ItemDestroyed);
		Assert.NotNull(result.ItemAfter);
		Assert.Empty(result.ItemAfter!.Liquids);
		Assert.Empty(result.TimedEffects);

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
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		var items = host.Services.GetRequiredService<IItemControl>();
		var hostSnapshot = SnapshotWithLimbs(HostId, conscious: true);
		hostSnapshot.Health!.BloodViscosity = 10f;
		characters.SaveHostCharacterData(hostSnapshot);
		var coagulant = MedicineBottle(42, "bloodcoagulant", "procoagulant", amount: 100f);
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, coagulant));
		items.AdoptTransferredItem(GuestId, 42, coagulant);

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendUseRequest(HostId, 42);

		var result = UseResult(received);
		Assert.Equal(GuestId, result.UserSteamId);
		Assert.Equal(HostId, result.TargetSteamId);
		Assert.False(result.ItemDestroyed);
		Assert.NotNull(result.ItemAfter);
		Assert.True(Math.Abs(result.ItemAfter!.Liquids.Single().Amount - 66.666f) < 0.001f);

		var timedBody = Assert.Single(result.TimedBodyEffects);
		Assert.Equal("procoagulant", timedBody.EffectId);
		Assert.True(Math.Abs(timedBody.DurationSeconds - 20f) < 0.01f);
		Assert.Empty(result.TimedEffects);
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

	// ---- Direct line-of-sight gate ----

	[Fact]
	public void Take_BlockedByLineOfSight_IsRefused()
	{
		var (host, guest, received) = CreateBlockedSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: false, Item(42)));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendTakeRequest(HostId, 42);

		Assert.DoesNotContain(KernelEvents(received), e => e.Kind == WireEventKind.PlayerInventoryTransfer);
		Assert.Contains(characters.GetHostCharacterData()!.Items, i => i.InstanceId == 42);
	}

	[Fact]
	public void Carry_BlockedByLineOfSight_IsRefused()
	{
		var (host, guest, received) = CreateBlockedSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: false));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendCarryStartRequest(HostId);

		Assert.Empty(CarryStates(received));
		Assert.False(host.Services.GetRequiredService<IPlayerInteractionControl>().TryGetCarried(GuestId, out _));
	}

	[Fact]
	public void Heal_BlockedByLineOfSight_IsRefused()
	{
		var (host, guest, received) = CreateBlockedSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: false));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, Item(42, "bandage", slot: 0)));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendHealRequest(HostId);

		Assert.DoesNotContain(KernelEvents(received), e => e.Kind == WireEventKind.PlayerHealResult);
		Assert.Contains(characters.GetSavedCharacter(GuestId)!.Items, i => i.InstanceId == 42);
	}

	[Fact]
	public void Use_BlockedByLineOfSight_IsRefused()
	{
		var (host, guest, received) = CreateBlockedSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(SnapshotWithLimbs(HostId, conscious: true));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, WaterBottle(42)));

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendUseRequest(HostId, 42);

		Assert.DoesNotContain(KernelEvents(received), e => e.Kind == WireEventKind.PlayerItemUseResult);
		Assert.Contains(characters.GetSavedCharacter(GuestId)!.Items, i => i.InstanceId == 42);
	}

	[Fact]
	public void Push_BlockedByLineOfSight_IsRefused()
	{
		var (host, guest, received) = CreateBlockedSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));
		SeedHostEntities(host, GuestId, guestX: 10f);

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendPushRequest(HostId);

		Assert.DoesNotContain(received, r => r.Msg == NetMsg.PlayerPushResult);
	}

	// ---- Remote backpack native operations ----

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

	[Fact]
	public void HostOwner_NativeUseRequest_RaisesApplyOnHost()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true, Item(42, "waterbottle")));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		RemoteInventoryApplyMsg? apply = null;
		host.Services.GetRequiredService<IPlayerInteractionControl>().RemoteInventoryApplyReceived += m => apply = m;

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendRemoteInventoryOperation(new RemoteInventoryOperationRequestMsg
			{
				Kind = RemoteInventoryOperationKind.Use,
				OwnerSteamId = HostId,
				ItemInstanceId = 42,
			});

		Assert.NotNull(apply);
		Assert.Equal(RemoteInventoryOperationKind.Use, apply!.Kind);
		Assert.Equal(HostId, apply.OwnerSteamId);
		Assert.Equal(42UL, apply.ItemInstanceId);
		Assert.Contains(characters.GetHostCharacterData()!.Items, i => i.InstanceId == 42);
	}

	[Fact]
	public void GuestOwner_NativeUseRequest_HostSendsApplyToGuest()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true, Item(42, "waterbottle")));

		host.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendRemoteInventoryOperation(new RemoteInventoryOperationRequestMsg
			{
				Kind = RemoteInventoryOperationKind.Use,
				OwnerSteamId = GuestId,
				ItemInstanceId = 42,
			});

		var frame = received.Single(r => r.Msg == NetMsg.RemoteInventoryApply).Frame;
		var apply = NetPacket.DecodePayload<RemoteInventoryApplyMsg>(frame);
		Assert.Equal(RemoteInventoryOperationKind.Use, apply.Kind);
		Assert.Equal(GuestId, apply.OwnerSteamId);
		Assert.Equal(42UL, apply.ItemInstanceId);
	}

	[Fact]
	public void NativeCombine_MissingSecondItem_IsRefusedWithoutApply()
	{
		var (host, guest, received) = CreateSession();
		var characters = host.Services.GetRequiredService<ICharacterDataControl>();
		characters.SaveHostCharacterData(Snapshot(HostId, conscious: true, Item(42)));
		characters.SaveCharacterData(GuestId, Snapshot(GuestId, conscious: true));

		RemoteInventoryApplyMsg? apply = null;
		host.Services.GetRequiredService<IPlayerInteractionControl>().RemoteInventoryApplyReceived += m => apply = m;

		guest.Services.GetRequiredService<IPlayerInteractionControl>()
			.SendRemoteInventoryOperation(new RemoteInventoryOperationRequestMsg
			{
				Kind = RemoteInventoryOperationKind.Combine,
				OwnerSteamId = HostId,
				ItemInstanceId = 42,
				TargetItemInstanceId = 999,
			});

		Assert.Null(apply);
		Assert.DoesNotContain(received, r => r.Msg == NetMsg.RemoteInventoryApply);
	}

	[Fact]
	public void RemoteOperation_BlockedByLineOfSight_IsRefused()
	{
		var (host, guest, received) = CreateBlockedSession();
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

		Assert.DoesNotContain(KernelEvents(received), e => e.Kind is WireEventKind.ItemRelocated or WireEventKind.PlayerInventoryTransfer);
		Assert.Contains(characters.GetHostCharacterData()!.Items, i => i.InstanceId == 42);
	}

	[Fact]
	public void HealRequest_RoundTripsSelectedLimbIndex()
	{
		var msg = new PlayerHealRequestMsg
		{
			TargetSteamId = HostId,
			ItemInstanceId = 42,
			LimbIndex = 0,
		};

		var decoded = NetPacket.DecodePayload<PlayerHealRequestMsg>(
			NetPacket.Encode(NetMsg.PlayerHealRequest, msg));

		Assert.Equal(HostId, decoded.TargetSteamId);
		Assert.Equal(42UL, decoded.ItemInstanceId);
		Assert.Equal(0, decoded.LimbIndex);
	}

	[Fact]
	public void HealRequest_RoundTripsAutoLimbSelection()
	{
		var msg = new PlayerHealRequestMsg
		{
			TargetSteamId = HostId,
			ItemInstanceId = 42,
			LimbIndex = -1,
		};

		var decoded = NetPacket.DecodePayload<PlayerHealRequestMsg>(
			NetPacket.Encode(NetMsg.PlayerHealRequest, msg));

		Assert.Equal(-1, decoded.LimbIndex);
	}

	[Fact]
	public void UseRequest_RoundTripsSelectedLimbIndex()
	{
		var msg = new PlayerItemUseRequestMsg
		{
			TargetSteamId = HostId,
			ItemInstanceId = 42,
			LimbIndex = 2,
		};

		var decoded = NetPacket.DecodePayload<PlayerItemUseRequestMsg>(
			NetPacket.Encode(NetMsg.PlayerItemUseRequest, msg));

		Assert.Equal(HostId, decoded.TargetSteamId);
		Assert.Equal(42UL, decoded.ItemInstanceId);
		Assert.Equal(2, decoded.LimbIndex);
	}

	private static (TestNode Host, TestNode Guest, List<(NetMsg Msg, byte[] Frame)> Received) CreateBlockedSession() =>
		CreateSession(s => s.Replace(
			ServiceDescriptor.Singleton<IPlayerInteractionVisibility>(
				new BlockingPlayerInteractionVisibility())));

	private sealed class BlockingPlayerInteractionVisibility : IPlayerInteractionVisibility
	{
		public bool HasLineOfSight(ulong observerSteamId, ulong targetSteamId) => false;
	}
}
