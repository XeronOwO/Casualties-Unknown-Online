using System.Collections.Generic;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Protocol.Versioning;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

public class KernelProtocolServiceTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	[Fact]
	public void Host_ExecutesCommandEnvelope_AndGuestAppliesBroadcastBatch()
	{
		var (network, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];
		guest.Steam.FireLobbyEntered(LobbyId);

		var kernel = host.Services.GetRequiredService<IKernelProtocolControl>();
		var frame = new ProtocolFrame
		{
			Kind = EnvelopeKind.Command,
			Command = new CommandEnvelope
			{
				Header = new EnvelopeHeader
				{
					ProtocolVersion = ProtocolConstants.EnvelopeVersion,
					RunEpoch = 1,
					SenderId = GuestId,
					OperationId = 100,
					PayloadType = WirePayloadType.ItemSpawnCommand,
				},
				Command = new WireCommand
				{
					Kind = WireCommandKind.ItemSpawn,
					Identity = new WireItemIdentity { InstanceId = 42, DefinitionId = "water" },
					Location = new WireItemLocation { Kind = WireItemLocationKind.World, X = 1f, Y = 2f },
					Data = new WireItemData { Condition = 0.8f, SlotIndex = -1 },
				},
			},
		};

		kernel.HandleFrame(GuestId, frame);

		var hostAuthority = host.Services.GetRequiredService<ItemKernelAuthority>();
		Assert.NotNull(hostAuthority.FindItem(42));

		// The fake network delivers synchronously; the guest must have applied
		// the broadcast committed batch to its replay kernel and projected the
		// world item into the legacy world-item table.
		var guestAuthority = guest.Services.GetRequiredService<ItemKernelAuthority>();
		Assert.NotNull(guestAuthority.FindItem(42));
		var guestWorld = guest.Services.GetRequiredService<ItemService>().GetWorldItemsForDiagnostics();
		Assert.Contains(guestWorld, w => w.ItemId == 42);
	}

	[Fact]
	public void Host_SendsCheckpoint_AndGuestRestoresItemState()
	{
		var (_, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];
		guest.Steam.FireLobbyEntered(LobbyId);

		var hostAuthority = host.Services.GetRequiredService<ItemKernelAuthority>();
		var kernel = host.Services.GetRequiredService<IKernelProtocolControl>();
		hostAuthority.ObserveSpawn(HostId, 42, "water", 1f, 2f);

		kernel.SendCheckpoint(GuestId);

		var guestAuthority = guest.Services.GetRequiredService<ItemKernelAuthority>();
		var item = guestAuthority.FindItem(42);
		Assert.NotNull(item);
		Assert.Equal(ItemLocationKind.World, item!.Value.Location.Kind);
	}

	[Fact]
	public void Host_BroadcastUnderDuplicateDelivery_GuestAppliesOnlyOnce()
	{
		var (network, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];
		guest.Steam.FireLobbyEntered(LobbyId);
		network.SetFaults(HostId, GuestId, new LinkFaults { Duplicate = true });

		var kernel = host.Services.GetRequiredService<IKernelProtocolControl>();
		kernel.HandleFrame(GuestId, SpawnCommandFrame());

		var guestWorld = guest.Services.GetRequiredService<ItemService>().GetWorldItemsForDiagnostics();
		Assert.Single(guestWorld);
		Assert.Single(guest.Services.GetRequiredService<ItemKernelAuthority>().QueryItems());
	}

	[Fact]
	public void Guest_DropsBatchWithRevisionGap()
	{
		var (_, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];
		guest.Steam.FireLobbyEntered(LobbyId);

		var kernel = guest.Services.GetRequiredService<IKernelProtocolControl>();
		kernel.HandleFrame(HostId, new ProtocolFrame
		{
			Kind = EnvelopeKind.CommittedBatch,
			CommittedBatch = new CommittedBatchEnvelope
			{
				Header = new EnvelopeHeader
				{
					ProtocolVersion = ProtocolConstants.EnvelopeVersion,
					RunEpoch = 1,
					SenderId = HostId,
					OperationId = 7,
					PayloadType = WirePayloadType.CommittedBatch,
				},
				Batch = SpawnWireBatch(globalRevision: 5),
			},
		});

		Assert.Null(guest.Services.GetRequiredService<ItemKernelAuthority>().FindItem(42));
	}

	[Fact]
	public void Guest_DropsBatchFromWrongEpoch()
	{
		var (_, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];
		guest.Steam.FireLobbyEntered(LobbyId);

		var kernel = guest.Services.GetRequiredService<IKernelProtocolControl>();
		kernel.HandleFrame(HostId, new ProtocolFrame
		{
			Kind = EnvelopeKind.CommittedBatch,
			CommittedBatch = new CommittedBatchEnvelope
			{
				Header = new EnvelopeHeader
				{
					ProtocolVersion = ProtocolConstants.EnvelopeVersion,
					RunEpoch = 999,
					SenderId = HostId,
					OperationId = 7,
					PayloadType = WirePayloadType.CommittedBatch,
				},
				Batch = SpawnWireBatch(globalRevision: 1, runEpoch: 999),
			},
		});

		Assert.Null(guest.Services.GetRequiredService<ItemKernelAuthority>().FindItem(42));
	}

	[Fact]
	public void Guest_DropsUnsupportedEnvelopeVersion()
	{
		var (_, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];
		guest.Steam.FireLobbyEntered(LobbyId);

		var kernel = guest.Services.GetRequiredService<IKernelProtocolControl>();
		kernel.HandleFrame(HostId, new ProtocolFrame
		{
			Kind = EnvelopeKind.CommittedBatch,
			CommittedBatch = new CommittedBatchEnvelope
			{
				Header = new EnvelopeHeader
				{
					ProtocolVersion = 999,
					RunEpoch = 1,
					SenderId = HostId,
					OperationId = 7,
					PayloadType = WirePayloadType.CommittedBatch,
				},
				Batch = SpawnWireBatch(globalRevision: 1),
			},
		});

		Assert.Null(guest.Services.GetRequiredService<ItemKernelAuthority>().FindItem(42));
	}

	[Fact]
	public void Host_DropsCommandFromWrongRunEpoch()
	{
		var (_, host, _) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);

		var kernel = host.Services.GetRequiredService<IKernelProtocolControl>();
		var frame = new ProtocolFrame
		{
			Kind = EnvelopeKind.Command,
			Command = new CommandEnvelope
			{
				Header = new EnvelopeHeader
				{
					ProtocolVersion = ProtocolConstants.EnvelopeVersion,
					RunEpoch = 999,
					SenderId = GuestId,
					OperationId = 100,
					PayloadType = WirePayloadType.ItemSpawnCommand,
				},
				Command = new WireCommand
				{
					Kind = WireCommandKind.ItemSpawn,
					Identity = new WireItemIdentity { InstanceId = 42, DefinitionId = "water" },
					Location = new WireItemLocation { Kind = WireItemLocationKind.World, X = 1f, Y = 2f },
				},
			},
		};

		kernel.HandleFrame(GuestId, frame);

		var authority = host.Services.GetRequiredService<ItemKernelAuthority>();
		Assert.Null(authority.FindItem(42));
	}

	[Fact]
	public void Guest_ItemSpawnReport_RidesCommandEnvelope_AndHostBroadcastsBatch()
	{
		var (_, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];
		guest.Steam.FireLobbyEntered(LobbyId);

		guest.Services.GetRequiredService<ItemService>().SendItemSpawned(
			42,
			new CharacterItemMsg { ItemId = "water", Condition = 0.8f },
			new NetVector2(1f, 2f),
			NetVector2.Zero,
			0f,
			false,
			0f);

		var hostAuthority = host.Services.GetRequiredService<ItemKernelAuthority>();
		Assert.NotNull(hostAuthority.FindItem(42));
		var guestAuthority = guest.Services.GetRequiredService<ItemKernelAuthority>();
		Assert.NotNull(guestAuthority.FindItem(42));
		var guestWorld = guest.Services.GetRequiredService<ItemService>().GetWorldItemsForDiagnostics();
		Assert.Contains(guestWorld, w => w.ItemId == 42);
	}

	[Fact]
	public void Guest_ItemPickupReport_RidesCommandEnvelope_AndHostResolvesRevision()
	{
		var (_, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];
		guest.Steam.FireLobbyEntered(LobbyId);

		var hostAuthority = host.Services.GetRequiredService<ItemKernelAuthority>();
		hostAuthority.ObserveSpawn(HostId, 42, "water", 1f, 2f);
		Assert.NotNull(hostAuthority.FindItem(42));

		var hostCommands = new List<ProtocolFrame>();
		host.Transport.MessageReceived += (_, frame) =>
		{
			if ((NetMsg)frame[0] == Runtime.Protocol.NetMsg.KernelEnvelope)
			{
				hostCommands.Add(NetPacket.DecodePayload<ProtocolFrame>(frame));
			}
		};

		guest.Services.GetRequiredService<ItemService>().SendItemPickedUp(42);
		Assert.NotEmpty(hostCommands);
		Assert.Equal(EnvelopeKind.Command, hostCommands[0].Kind);
		var commandEnvelope = hostCommands[0].Command!;
		Assert.NotNull(commandEnvelope);
		Assert.Equal(WireCommandKind.ItemPickup, commandEnvelope.Command.Kind);
		Assert.Equal(1ul, commandEnvelope.Header.RunEpoch);

		var hostItem = hostAuthority.FindItem(42)!.Value;
		Assert.Equal(ItemLocationKind.Carried, hostItem.Location.Kind);
		Assert.Equal(GuestId, hostItem.Location.Owner.Value);

		var guestAuthority = guest.Services.GetRequiredService<ItemKernelAuthority>();
		var guestItem = guestAuthority.FindItem(42)!.Value;
		Assert.Equal(ItemLocationKind.Carried, guestItem.Location.Kind);
		Assert.Empty(guest.Services.GetRequiredService<ItemService>().GetWorldItemsForDiagnostics());
	}

	[Fact]
	public void Guest_CheckpointRebuild_RestoresDroppedWorldProjection()
	{
		var (_, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];
		guest.Steam.FireLobbyEntered(LobbyId);

		var hostAuthority = host.Services.GetRequiredService<ItemKernelAuthority>();
		hostAuthority.ObserveSpawn(HostId, 42, "water", 1f, 2f);
		Assert.Contains(guest.Services.GetRequiredService<ItemService>().GetWorldItemsForDiagnostics(), w => w.ItemId == 42);

		// Drop the guest's world projection (simulates a projection failure).
		guest.Services.GetRequiredService<ItemService>().ResetItems();
		Assert.Empty(guest.Services.GetRequiredService<ItemService>().GetWorldItemsForDiagnostics());

		// A fresh checkpoint must rebuild the projection from authoritative state.
		var hostKernel = host.Services.GetRequiredService<IKernelProtocolControl>();
		hostKernel.SendCheckpoint(GuestId);

		Assert.Contains(guest.Services.GetRequiredService<ItemService>().GetWorldItemsForDiagnostics(), w => w.ItemId == 42);
	}

	[Fact]
	public void Disconnect_ThenReconnectWithCheckpoint_RestoresGuest()
	{
		var (network, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];
		guest.Steam.FireLobbyEntered(LobbyId);

		network.SetFaults(HostId, GuestId, new LinkFaults { Down = true });
		network.SetFaults(GuestId, HostId, new LinkFaults { Down = true });

		var hostAuthority = host.Services.GetRequiredService<ItemKernelAuthority>();
		hostAuthority.ObserveSpawn(HostId, 42, "water", 1f, 2f);
		Assert.Null(guest.Services.GetRequiredService<ItemKernelAuthority>().FindItem(42));

		network.ClearFaults(HostId, GuestId);
		network.ClearFaults(GuestId, HostId);

		host.Services.GetRequiredService<IKernelProtocolControl>().SendCheckpoint(GuestId);

		Assert.NotNull(guest.Services.GetRequiredService<ItemKernelAuthority>().FindItem(42));
		Assert.Contains(guest.Services.GetRequiredService<ItemService>().GetWorldItemsForDiagnostics(), w => w.ItemId == 42);
	}

	[Fact]
	public void KernelProtocol_UnderLatencyAndDuplicates_Converges()
	{
		var (network, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];
		guest.Steam.FireLobbyEntered(LobbyId);
		network.SetFaults(GuestId, HostId, new LinkFaults { DelayMs = 40, Duplicate = true });
		network.SetFaults(HostId, GuestId, new LinkFaults { DelayMs = 40, Duplicate = true });

		guest.Services.GetRequiredService<ItemService>().SendItemSpawned(
			42,
			new CharacterItemMsg { ItemId = "water", Condition = 0.8f },
			new NetVector2(1f, 2f),
			NetVector2.Zero,
			0f,
			false,
			0f);

		var driver = new SimulationDriver(guest.Clock, network, host, guest);
		var hostAuthority = host.Services.GetRequiredService<ItemKernelAuthority>();
		var guestAuthority = guest.Services.GetRequiredService<ItemKernelAuthority>();
		driver.TickUntil(() => hostAuthority.FindItem(42) is not null && guestAuthority.FindItem(42) is not null,
			maxMs: 1000);

		var guestWorld = guest.Services.GetRequiredService<ItemService>().GetWorldItemsForDiagnostics();
		Assert.Contains(guestWorld, w => w.ItemId == 42);
	}

	[Fact]
	public void Host_RangeRequest_SendsMissingJournalBatches()
	{
		var (_, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		var hostAuthority = host.Services.GetRequiredService<ItemKernelAuthority>();
		hostAuthority.ObserveSpawn(HostId, 42, "water", 1f, 2f);
		hostAuthority.ObserveSpawn(HostId, 43, "water", 3f, 4f);

		var received = new List<ProtocolFrame>();
		guest.Transport.MessageReceived += (_, frame) =>
		{
			if ((NetMsg)frame[0] == NetMsg.KernelEnvelope)
			{
				received.Add(NetPacket.DecodePayload<ProtocolFrame>(frame));
			}
		};

		var kernel = host.Services.GetRequiredService<IKernelProtocolControl>();
		kernel.HandleFrame(GuestId, RangeRequestFrame(1, 1));

		Assert.Contains(received, f => f.CommittedBatch?.Batch.GlobalRevision == 1);
		Assert.DoesNotContain(received, f => f.CommittedBatch?.Batch.GlobalRevision == 2);
	}

	[Fact]
	public void Guest_BuffersOutOfOrderBatch_AndAppliesAfterMissingRange()
	{
		var (_, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];
		guest.Steam.FireLobbyEntered(LobbyId);

		var kernel = guest.Services.GetRequiredService<IKernelProtocolControl>();
		kernel.HandleFrame(HostId, BatchFrame(SpawnWireBatch(globalRevision: 2, itemId: 43)));

		var guestAuthority = guest.Services.GetRequiredService<ItemKernelAuthority>();
		Assert.Null(guestAuthority.FindItem(43));

		kernel.HandleFrame(HostId, BatchFrame(SpawnWireBatch(globalRevision: 1, itemId: 42)));

		Assert.NotNull(guestAuthority.FindItem(42));
		Assert.NotNull(guestAuthority.FindItem(43));
	}

	[Fact]
	public void Host_DirectPickupCommand_ResolvesRevision()
	{
		var (_, host, _) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		var hostAuthority = host.Services.GetRequiredService<ItemKernelAuthority>();
		hostAuthority.ObserveSpawn(HostId, 42, "water", 1f, 2f);

		var kernel = host.Services.GetRequiredService<IKernelProtocolControl>();
		kernel.HandleFrame(GuestId, new ProtocolFrame
		{
			Kind = EnvelopeKind.Command,
			Command = new CommandEnvelope
			{
				Header = new EnvelopeHeader
				{
					ProtocolVersion = ProtocolConstants.EnvelopeVersion,
					RunEpoch = 1,
					SenderId = GuestId,
					OperationId = 200,
					PayloadType = WirePayloadType.ItemPickupCommand,
				},
				Command = new WireCommand
				{
					Kind = WireCommandKind.ItemPickup,
					Identity = new WireItemIdentity { InstanceId = 42, DefinitionId = "water" },
					NewOwner = GuestId,
					ExpectedRevision = 0,
				},
			},
		});

		Assert.Equal(ItemLocationKind.Carried, hostAuthority.FindItem(42)!.Value.Location.Kind);
	}

	[Fact]
	public void NetworkDeliveredPickupCommand_IsExecutedByHost()
	{
		var (network, host, _) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		var hostAuthority = host.Services.GetRequiredService<ItemKernelAuthority>();
		hostAuthority.ObserveSpawn(HostId, 42, "water", 1f, 2f);

		var frame = new ProtocolFrame
		{
			Kind = EnvelopeKind.Command,
			Command = new CommandEnvelope
			{
				Header = new EnvelopeHeader
				{
					ProtocolVersion = ProtocolConstants.EnvelopeVersion,
					RunEpoch = 1,
					SenderId = GuestId,
					OperationId = 200,
					PayloadType = WirePayloadType.ItemPickupCommand,
				},
				Command = new WireCommand
				{
					Kind = WireCommandKind.ItemPickup,
					Identity = new WireItemIdentity { InstanceId = 42, DefinitionId = "water" },
					NewOwner = GuestId,
					ExpectedRevision = 0,
				},
			},
		};

		network.Deliver(GuestId, HostId, NetPacket.Encode(NetMsg.KernelEnvelope, frame));

		Assert.Equal(ItemLocationKind.Carried, hostAuthority.FindItem(42)!.Value.Location.Kind);
	}

	private static ProtocolFrame RangeRequestFrame(ulong start, ulong end) =>
		new()
		{
			Kind = EnvelopeKind.Command,
			Command = new CommandEnvelope
			{
				Header = new EnvelopeHeader
				{
					ProtocolVersion = ProtocolConstants.EnvelopeVersion,
					RunEpoch = 1,
					SenderId = GuestId,
					OperationId = 300,
					PayloadType = WirePayloadType.RangeRequestCommand,
				},
				Command = new WireCommand
				{
					Kind = WireCommandKind.RangeRequest,
					RangeStart = start,
					RangeEnd = end,
				},
			},
		};

	private static ProtocolFrame BatchFrame(WireCommittedBatch batch) =>
		new()
		{
			Kind = EnvelopeKind.CommittedBatch,
			CommittedBatch = new CommittedBatchEnvelope
			{
				Header = new EnvelopeHeader
				{
					ProtocolVersion = ProtocolConstants.EnvelopeVersion,
					RunEpoch = batch.RunEpoch,
					SenderId = HostId,
					OperationId = batch.OperationId,
					PayloadType = WirePayloadType.CommittedBatch,
				},
				Batch = batch,
			},
		};

	private static ProtocolFrame SpawnCommandFrame() =>
		new()
		{
			Kind = EnvelopeKind.Command,
			Command = new CommandEnvelope
			{
				Header = new EnvelopeHeader
				{
					ProtocolVersion = ProtocolConstants.EnvelopeVersion,
					RunEpoch = 1,
					SenderId = GuestId,
					OperationId = 100,
					PayloadType = WirePayloadType.ItemSpawnCommand,
				},
				Command = new WireCommand
				{
					Kind = WireCommandKind.ItemSpawn,
					Identity = new WireItemIdentity { InstanceId = 42, DefinitionId = "water" },
					Location = new WireItemLocation { Kind = WireItemLocationKind.World, X = 1f, Y = 2f },
					Data = new WireItemData { Condition = 0.8f, SlotIndex = -1 },
				},
			},
		};

	private static WireCommittedBatch SpawnWireBatch(ulong globalRevision, ulong runEpoch = 1, ulong itemId = 42) =>
		new()
		{
			OperationId = globalRevision,
			GlobalRevision = globalRevision,
			Actor = HostId,
			Authority = (int)AuthorityKind.HostOnly,
			RunEpoch = runEpoch,
			Events =
			[
				new WireEvent
				{
					Kind = WireEventKind.ItemSpawned,
					Identity = new WireItemIdentity { InstanceId = itemId, DefinitionId = "water" },
					NewRevision = 1,
					NewLocation = new WireItemLocation { Kind = WireItemLocationKind.World, X = 1f, Y = 2f },
					NewData = new WireItemData { Condition = 0.8f },
				},
			],
		};
}
