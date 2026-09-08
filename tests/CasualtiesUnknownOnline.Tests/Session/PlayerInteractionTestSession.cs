using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The stateless fixture helpers shared by the player-interaction behavior families:
/// the message builders, the handshaken host+guest session and the kernel-event
/// projections. Every test still builds its own nodes — this class holds no state.
/// </summary>
internal static class PlayerInteractionTestSession
{
	internal const ulong HostId = 1001;

	internal const ulong GuestId = 2001;

	internal const ulong LobbyId = 9001;

	internal static CharacterItemMsg Item(ulong instanceId, string itemId = "medkit", int slot = 0) => new()
	{
		InstanceId = instanceId,
		ItemId = itemId,
		SlotIndex = slot,
		Condition = 0.75f,
		Favourited = true,
	};

	internal static CharacterDataMsg Snapshot(ulong owner, bool conscious, params CharacterItemMsg[] items) => new()
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

	internal static CharacterDataMsg SnapshotWithLimbs(ulong owner, bool conscious, bool alive = true, params CharacterItemMsg[] items)
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

	internal static void MarkInWorld(TestNode node) =>
		node.Session.ReportSceneState(SceneStateType.InWorld, "SampleScene");

	internal static (TestNode Host, TestNode Guest, List<(NetMsg Msg, byte[] Frame)> Received) CreateSession(
		Action<IServiceCollection>? extraRegistrations = null)
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId, extraRegistrations: extraRegistrations);
		var received = new List<(NetMsg Msg, byte[] Frame)>();
		guest.Transport.MessageReceived += (_, frame) => received.Add(((NetMsg)frame[0], frame));
		MarkInWorld(host);
		MarkInWorld(guest);
		return (host, guest, received);
	}

	internal static List<PlayerCarryStateMsg> CarryStates(IEnumerable<(NetMsg Msg, byte[] Frame)> received) =>
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

	internal static IEnumerable<WireEvent> KernelEvents(IEnumerable<(NetMsg Msg, byte[] Frame)> received) =>
		received
			.Where(r => r.Msg == NetMsg.KernelEnvelope)
			.Select(r => NetPacket.DecodePayload<ProtocolFrame>(r.Frame))
			.Where(f => f.CommittedBatch is not null)
			.SelectMany(f => f.CommittedBatch!.Batch.Events);

	internal static PlayerInventoryTransferMsg TransferResult(IEnumerable<(NetMsg Msg, byte[] Frame)> received) =>
		PlayerInteractionKernelCodec.ToTransferMessage(PlayerInteractionWireMapper.FromWireInventoryTransfer(
			KernelEvents(received).Single(e => e.Kind == WireEventKind.PlayerInventoryTransfer).PlayerInteraction!));

	internal static PlayerHealResultMsg HealResult(IEnumerable<(NetMsg Msg, byte[] Frame)> received) =>
		PlayerInteractionKernelCodec.ToHealMessage(PlayerInteractionWireMapper.FromWireHealResult(
			KernelEvents(received).Single(e => e.Kind == WireEventKind.PlayerHealResult).PlayerInteraction!));

	internal static PlayerItemUseResultMsg UseResult(IEnumerable<(NetMsg Msg, byte[] Frame)> received) =>
		PlayerInteractionKernelCodec.ToUseMessage(PlayerInteractionWireMapper.FromWireItemUseResult(
			KernelEvents(received).Single(e => e.Kind == WireEventKind.PlayerItemUseResult).PlayerInteraction!));

	internal static void SeedHostEntities(TestNode host, ulong guestId, float guestX, float guestY = 0f, bool standing = true)
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

	internal static CharacterItemMsg WaterBottle(ulong instanceId, float amount = 500f, float condition = 1f) => new()
	{
		InstanceId = instanceId,
		ItemId = "waterbottle",
		SlotIndex = 0,
		Condition = condition,
		Liquids = [new LiquidStackMsg { LiquidId = "water", Amount = amount }],
	};

	internal static CharacterItemMsg MedicineBottle(
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

	internal static CharacterItemMsg TopicalBottle(
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

	internal static (TestNode Host, TestNode Guest, List<(NetMsg Msg, byte[] Frame)> Received) CreateBlockedSession() =>
		CreateSession(s => s.Replace(
			ServiceDescriptor.Singleton<IPlayerInteractionVisibility>(
				new BlockingPlayerInteractionVisibility())));

	private sealed class BlockingPlayerInteractionVisibility : IPlayerInteractionVisibility
	{
		public bool HasLineOfSight(ulong observerSteamId, ulong targetSteamId) => false;
	}
}
