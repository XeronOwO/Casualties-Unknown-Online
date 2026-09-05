using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

public class ContainerSyncProtocolTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	[Fact]
	public void HostAuthority_AcceptsCarriedSpawn()
	{
		var (_, host, _) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];

		var authority = host.Services.GetRequiredService<ItemKernelAuthority>();
		var ok = authority.TrySpawnCarried(
			GuestId,
			777,
			"backpack",
			new CharacterItemMsg { InstanceId = 777, ItemId = "backpack", SlotIndex = 1 },
			out _,
			out var rejection);
		Assert.True(ok, rejection?.Message ?? "rejected without message");

		var command = new SpawnItemCommand(
			new OperationId(99),
			new ActorId(GuestId),
			authority.CreateCheckpoint().RunEpoch,
			AuthorityKind.OwnerPredictedHostValidated,
			new ItemIdentity(778, "backpack"),
			ItemLocation.Carried(new ActorId(GuestId)),
			0,
			ItemKernelAuthority.ToKernelData(new CharacterItemMsg { InstanceId = 778, ItemId = "backpack", SlotIndex = 1 }));
		var ok2 = authority.TryExecuteCommand(command, GuestId, out _, out var rejection2);
		Assert.True(ok2, rejection2?.Message ?? "TryExecuteCommand rejected without message");
	}

	[Fact]
	public void GuestItemSpawn_CarriesTransientInitialDropStateOnTheWire()
	{
		var (network, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];
		guest.Steam.FireLobbyEntered(LobbyId);

		var hostFrames = new List<ProtocolFrame>();
		host.Transport.MessageReceived += (_, frame) =>
		{
			if ((NetMsg)frame[0] == NetMsg.KernelEnvelope)
			{
				hostFrames.Add(NetPacket.DecodePayload<ProtocolFrame>(frame));
			}
		};

		guest.Services.GetRequiredService<IItemControl>().SendItemSpawned(
			777,
			new CharacterItemMsg { InstanceId = 777, ItemId = "metalscrap", Condition = 1f },
			new NetVector2(10f, 20f),
			new NetVector2(3.5f, -2f),
			45f,
			freshItemDrop: true,
			angularVelocity: 8f);

		var spawnFrame = hostFrames.Single(f => f.Command?.Command.Kind == WireCommandKind.ItemSpawn);
		var command = spawnFrame.Command!.Command;
		Assert.Equal(3.5f, command.VelocityX);
		Assert.Equal(-2f, command.VelocityY);
		Assert.Equal(45f, command.Rotation);
		Assert.True(command.FreshItemDrop);
		Assert.Equal(8f, command.AngularVelocity);
	}

	[Fact]
	public void GuestItemSpawn_PresentationFlowsThroughHostBroadcastToGuestProjection()
	{
		var (network, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];
		guest.Steam.FireLobbyEntered(LobbyId);

		var guestProjected = new List<WorldItem>();
		guest.Services.GetRequiredService<IItemControl>().ItemSpawned += item => guestProjected.Add(item);

		guest.Services.GetRequiredService<IItemControl>().SendItemSpawned(
			777,
			new CharacterItemMsg { InstanceId = 777, ItemId = "metalscrap", Condition = 1f },
			new NetVector2(10f, 20f),
			new NetVector2(3.5f, -2f),
			45f,
			freshItemDrop: true,
			angularVelocity: 8f);

		var projected = Assert.Single(guestProjected);
		Assert.True(projected.FreshItemDrop);
		Assert.Equal(3.5f, projected.Vel.X);
		Assert.Equal(-2f, projected.Vel.Y);
		Assert.Equal(45f, projected.Rotation);
		Assert.Equal(8f, projected.AngularVelocity);
	}

	[Fact]
	public void ContainerSyncCommand_CreatesCarriedFact_AndGuestReceivesCarriedSync()
	{
		var (network, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];
		guest.Steam.FireLobbyEntered(LobbyId);

		var guestFacts = new List<(ulong Owner, CharacterItemMsg Item)>();
		guest.Services.GetRequiredService<IItemControl>().ItemCarriedSyncReceived += (owner, item, _) => guestFacts.Add((owner, item));

		var hostFrames = new List<ProtocolFrame>();
		host.Transport.MessageReceived += (_, frame) =>
		{
			if ((NetMsg)frame[0] == NetMsg.KernelEnvelope)
			{
				hostFrames.Add(NetPacket.DecodePayload<ProtocolFrame>(frame));
			}
		};

		guest.Services.GetRequiredService<IItemControl>().SendItemContainerContent(
			777,
			new CharacterItemMsg
			{
				InstanceId = 777,
				ItemId = "backpack",
				SlotIndex = 1,
				Contents = [new CharacterItemMsg { InstanceId = 888, ItemId = "bandage" }],
			});

		Assert.True(hostFrames.Count > 0, "host must receive a kernel envelope command");
		Assert.Equal(WireCommandKind.ItemContainerSync, hostFrames[0].Command!.Command.Kind);
		var hostAuthority = host.Services.GetRequiredService<ItemKernelAuthority>();
		Assert.NotNull(hostAuthority.FindItem(777));
		Assert.NotNull(hostAuthority.FindItem(888));
		var fact = Assert.Single(guestFacts);
		Assert.Equal(GuestId, fact.Owner);
		Assert.Contains(fact.Item.Contents, c => c.InstanceId == 888);
	}
}
