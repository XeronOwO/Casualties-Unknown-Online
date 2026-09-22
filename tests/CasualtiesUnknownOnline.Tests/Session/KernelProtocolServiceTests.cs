using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Protocol.Versioning;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.ProjectionHealth;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using CasualtiesUnknownOnline.Application.Kernel;

namespace CasualtiesUnknownOnline.Tests.Session;

[Trait("Category", "Integration")]
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
		Assert.Contains(host.Services.GetRequiredService<ItemService>().GetWorldItemsForDiagnostics(), w => w.ItemId == 42);
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
			if ((NetMsg)frame[0] == NetMsg.KernelEnvelope)
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
	public void ProjectionFailure_MarksDirtyAndRebuildsWithoutRevertingKernel()
	{
		var (_, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];
		guest.Steam.FireLobbyEntered(LobbyId);

		var items = guest.Services.GetRequiredService<ItemService>();
		var allowFailure = true;
		items.ItemSpawned += _ =>
		{
			if (allowFailure)
			{
				throw new InvalidOperationException("projection failure");
			}
		};

		var kernel = guest.Services.GetRequiredService<IKernelProtocolControl>();
		kernel.HandleFrame(HostId, BatchFrame(SpawnWireBatch(globalRevision: 1, itemId: 42)));

		Assert.NotNull(guest.Services.GetRequiredService<ItemKernelAuthority>().FindItem(42));

		var health = guest.Services.GetRequiredService<ProjectionHealthCoordinator>();
		Assert.True(health.IsDirty("items"), "projection failure must mark the item domain dirty");

		// Simulate a stale Unity projection, then let the main-thread pump rebuild it.
		items.ResetItems();
		allowFailure = false;
		guest.Update();

		Assert.Contains(items.GetWorldItemsForDiagnostics(), w => w.ItemId == 42);
		Assert.False(health.IsDirty("items"), "successful rebuild must clear the dirty marker");
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

	[Fact]
	public void Guest_StaleItemSnapshotOlderThanAppliedKernelRevision_IsDropped()
	{
		var (_, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		guest.Steam.FireLobbyEntered(LobbyId);

		var kernel = guest.Services.GetRequiredService<IKernelProtocolControl>();
		kernel.HandleFrame(HostId, BatchFrame(SpawnWireBatch(globalRevision: 1, itemId: 42)));

		var received = new List<IReadOnlyList<WorldItem>>();
		guest.Services.GetRequiredService<IItemControl>().ItemSnapshotReceived += (items, _, _) => received.Add(items);

		kernel.HandleFrame(HostId, SnapshotFrame(WirePayloadType.ItemSnapshotStream, seq: 1, baseRevision: 0, SnapshotItem(42)));

		Assert.Empty(received);
	}

	[Fact]
	public void Guest_OutOfOrderItemSnapshot_IsDroppedBySeq()
	{
		var (_, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		guest.Steam.FireLobbyEntered(LobbyId);

		var kernel = guest.Services.GetRequiredService<IKernelProtocolControl>();
		var received = new List<IReadOnlyList<WorldItem>>();
		guest.Services.GetRequiredService<IItemControl>().ItemSnapshotReceived += (items, _, _) => received.Add(items);

		kernel.HandleFrame(HostId, SnapshotFrame(WirePayloadType.ItemSnapshotStream, seq: 2, baseRevision: 0, SnapshotItem(42)));
		kernel.HandleFrame(HostId, SnapshotFrame(WirePayloadType.ItemSnapshotStream, seq: 1, baseRevision: 0, SnapshotItem(42)));

		var entry = Assert.Single(received);
		Assert.Equal(42ul, entry[0].ItemId);
	}

	[Fact]
	public void Host_ItemSnapshotBroadcast_CarriesMonotonicEventVersion()
	{
		var (network, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];
		guest.Steam.FireLobbyEntered(LobbyId);
		var driver = new SimulationDriver(host.Clock, network, host, guest);
		driver.TickUntil(() => host.Session.Members.Count(m => m.Handshaken) == 1, maxMs: 1000);

		var guestStreams = new List<WireStateStream>();
		guest.Services.GetRequiredService<IKernelProtocolControl>().ItemStateStreamReceived += (_, stream) => guestStreams.Add(stream);

		var hostKernel = host.Services.GetRequiredService<IKernelProtocolControl>();
		hostKernel.BroadcastItemStateStream([SnapshotItem(42)], WirePayloadType.ItemSnapshotStream, reliable: true);
		hostKernel.BroadcastItemStateStream([SnapshotItem(43)], WirePayloadType.ItemSnapshotStream, reliable: true);

		Assert.Equal(2, guestStreams.Count);
		Assert.Equal(1u, guestStreams[0].Seq);
		Assert.Equal(2u, guestStreams[1].Seq);
	}

	[Fact]
	public void Guest_DropsStateStreamWithStaleRunEpoch()
	{
		var (_, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];
		guest.Steam.FireLobbyEntered(LobbyId);

		var guestKernel = guest.Services.GetRequiredService<IKernelProtocolControl>();
		var received = new List<WireStateStream>();
		guestKernel.ItemStateStreamReceived += (_, stream) => received.Add(stream);

		var frame = SnapshotFrame(WirePayloadType.ItemSnapshotStream, seq: 1, baseRevision: 0, SnapshotItem(42));
		frame.StateStream!.Header.RunEpoch = 0; // a frame from a previous run

		guestKernel.HandleFrame(HostId, frame);

		Assert.Empty(received);
	}

	[Fact]
	public void Guest_AcceptsStateStreamAfterRestoringAForeignRunEpoch()
	{
		var (_, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];
		guest.Steam.FireLobbyEntered(LobbyId);

		// The host ran a previous session, so its epoch is 2 while this guest
		// process starts at 1. The guest adopts the restored checkpoint's epoch.
		var guestAuthority = guest.Services.GetRequiredService<ItemKernelAuthority>();
		var foreignCheckpoint = new GameStateKernel(new RunEpoch(2)).CreateCheckpoint();
		Assert.True(guestAuthority.Restore(foreignCheckpoint).Success);

		var guestKernel = guest.Services.GetRequiredService<IKernelProtocolControl>();
		var received = new List<WireStateStream>();
		guestKernel.ItemStateStreamReceived += (_, stream) => received.Add(stream);

		var frame = SnapshotFrame(WirePayloadType.ItemSnapshotStream, seq: 1, baseRevision: 0, SnapshotItem(42));
		frame.StateStream!.Header.RunEpoch = 2;

		guestKernel.HandleFrame(HostId, frame);

		Assert.Single(received);
	}

	[Fact]
	public void Guest_AcceptsStateStreamAfterARealCheckpointRestore()
	{
		var (_, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];
		guest.Steam.FireLobbyEntered(LobbyId);

		// The host is on its second run (epoch 2); this guest process starts at 1
		// and restores through the real chunked checkpoint path.
		host.Services.GetRequiredService<ItemKernelAuthority>().ResetSessionState();
		host.Services.GetRequiredService<IKernelProtocolControl>().SendCheckpoint(GuestId);

		var guestKernel = guest.Services.GetRequiredService<IKernelProtocolControl>();
		var received = new List<WireStateStream>();
		guestKernel.ItemStateStreamReceived += (_, stream) => received.Add(stream);

		var frame = SnapshotFrame(WirePayloadType.ItemSnapshotStream, seq: 1, baseRevision: 0, SnapshotItem(42));
		frame.StateStream!.Header.RunEpoch = 2;

		guestKernel.HandleFrame(HostId, frame);

		Assert.Single(received);
	}

	[Fact]
	public void Guest_ServesTheRunTheWorldJoinAnnounced()
	{
		var (_, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];
		guest.Steam.FireLobbyEntered(LobbyId);

		// The host is on its second run and says so before the entry group: the
		// instruction carries the identity the member validates that set against.
		var hostAuthority = host.Services.GetRequiredService<ItemKernelAuthority>();
		hostAuthority.ResetSessionState();
		hostAuthority.ObserveSpawn(HostId, 77, "water", 3f, 4f);
		host.Services.GetRequiredService<IWorldControl>().SendWorldJoin(isTutorial: false);
		host.Services.GetRequiredService<IKernelProtocolControl>().SendCheckpoint(GuestId);

		var guestAuthority = guest.Services.GetRequiredService<ItemKernelAuthority>();
		Assert.NotNull(guestAuthority.FindItem(77));
		Assert.Equal(2UL, guestAuthority.CurrentRunEpoch.Value);
	}

	[Fact]
	public void Guest_RefusesCheckpointSetFromAnotherRun()
	{
		var (_, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];
		guest.Steam.FireLobbyEntered(LobbyId);

		// Run 1's checkpoint, captured before the host moved on — what a straggler
		// set carries.
		var hostAuthority = host.Services.GetRequiredService<ItemKernelAuthority>();
		hostAuthority.ObserveSpawn(HostId, 42, "water", 1f, 2f);
		var previousRun = WireCheckpointAssembler.Split(hostAuthority.CreateCheckpoint(), TestKernelCodec.Instance);

		hostAuthority.ResetSessionState();
		hostAuthority.ObserveSpawn(HostId, 77, "water", 3f, 4f);
		host.Services.GetRequiredService<IWorldControl>().SendWorldJoin(isTutorial: false);
		var hostKernel = host.Services.GetRequiredService<IKernelProtocolControl>();
		var guestKernel = guest.Services.GetRequiredService<IKernelProtocolControl>();
		hostKernel.SendCheckpoint(GuestId);

		var guestAuthority = guest.Services.GetRequiredService<ItemKernelAuthority>();
		Assert.NotNull(guestAuthority.FindItem(77));

		// The straggler arrives whole, after the live run is already established.
		foreach (var chunk in previousRun)
		{
			guestKernel.HandleFrame(HostId, CheckpointFrame(chunk));
		}

		Assert.Null(guestAuthority.FindItem(42)); // the old run's rows never land
		Assert.NotNull(guestAuthority.FindItem(77)); // the live run's state is untouched
		Assert.Equal(2UL, guestAuthority.CurrentRunEpoch.Value); // and the identity did not move back

		// The harm was this side rejecting everything the run it is in sends: an
		// adopted stale epoch would drop the live run's stream below.
		var received = new List<WireStateStream>();
		guestKernel.ItemStateStreamReceived += (_, stream) => received.Add(stream);
		var stream = SnapshotFrame(WirePayloadType.ItemSnapshotStream, seq: 1, baseRevision: 0, SnapshotItem(7));
		stream.StateStream!.Header.RunEpoch = 2;
		guestKernel.HandleFrame(HostId, stream);

		Assert.Single(received);
	}

	[Fact]
	public void Guest_RefusesASetFromARunTheWorldJoinDidNotAnnounce()
	{
		var (_, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];
		guest.Steam.FireLobbyEntered(LobbyId);

		var hostAuthority = host.Services.GetRequiredService<ItemKernelAuthority>();
		hostAuthority.ResetSessionState();
		hostAuthority.ObserveSpawn(HostId, 42, "water", 1f, 2f);

		// The instruction is what decides, not the set's own stamp and not this side's
		// epoch. It is driven as a frame here because a real host stamps instruction and
		// set from the same instant; the production stamping itself is pinned by
		// Guest_ServesTheRunTheWorldJoinAnnounced.
		host.Services.GetRequiredService<PacketSender>().Send(GuestId, NetMsg.WorldJoin, new WorldJoinMsg { IsTutorial = false, RunEpoch = 3 });

		var guestKernel = guest.Services.GetRequiredService<IKernelProtocolControl>();
		foreach (var chunk in WireCheckpointAssembler.Split(hostAuthority.CreateCheckpoint(), TestKernelCodec.Instance))
		{
			guestKernel.HandleFrame(HostId, CheckpointFrame(chunk));
		}

		// Nothing was restored: this side serves run 3 and the set is not it.
		var guestAuthority = guest.Services.GetRequiredService<ItemKernelAuthority>();
		Assert.Null(guestAuthority.FindItem(42));
		Assert.Equal(1UL, guestAuthority.CurrentRunEpoch.Value);
	}

	[Fact]
	public void Guest_AdoptsTheIdentityFromTheFirstRestoredSet()
	{
		var (_, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];
		guest.Steam.FireLobbyEntered(LobbyId);

		var hostAuthority = host.Services.GetRequiredService<ItemKernelAuthority>();
		hostAuthority.ObserveSpawn(HostId, 42, "water", 1f, 2f);
		var previousRun = WireCheckpointAssembler.Split(hostAuthority.CreateCheckpoint(), TestKernelCodec.Instance);

		// Run 2 reaches this side with NO instruction in front of it (a reconnect's
		// entry group is sent before the join): that set defines the identity, and the
		// set that follows it is compared against what it established.
		hostAuthority.ResetSessionState();
		hostAuthority.ObserveSpawn(HostId, 77, "water", 3f, 4f);
		var guestKernel = guest.Services.GetRequiredService<IKernelProtocolControl>();
		host.Services.GetRequiredService<IKernelProtocolControl>().SendCheckpoint(GuestId);

		var guestAuthority = guest.Services.GetRequiredService<ItemKernelAuthority>();
		Assert.NotNull(guestAuthority.FindItem(77));
		Assert.Equal(2UL, guestAuthority.CurrentRunEpoch.Value);

		foreach (var chunk in previousRun)
		{
			guestKernel.HandleFrame(HostId, CheckpointFrame(chunk));
		}

		Assert.Null(guestAuthority.FindItem(42));
		Assert.NotNull(guestAuthority.FindItem(77));
	}

	[Fact]
	public void Guest_StaleChunkDoesNotBlockTheLiveCheckpointSet()
	{
		var (network, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];
		guest.Steam.FireLobbyEntered(LobbyId);

		var hostAuthority = host.Services.GetRequiredService<ItemKernelAuthority>();
		hostAuthority.ResetSessionState();
		hostAuthority.ObserveSpawn(HostId, 77, "water", 3f, 4f);
		host.Services.GetRequiredService<IWorldControl>().SendWorldJoin(isTutorial: false);
		var hostKernel = host.Services.GetRequiredService<IKernelProtocolControl>();
		var guestKernel = guest.Services.GetRequiredService<IKernelProtocolControl>();
		hostKernel.SendCheckpoint(GuestId);

		var guestAuthority = guest.Services.GetRequiredService<ItemKernelAuthority>();
		Assert.NotNull(guestAuthority.FindItem(77));

		// A straggler of the previous run, trailing chunk only. Kept, it occupies an
		// index the live set never fills, so the count check can never be satisfied
		// for any later set — the member then never restores again.
		guestKernel.HandleFrame(HostId, CheckpointFrame(TrailingChunkOfASupersededSet(runEpoch: 1, globalRevision: 3)));

		// The live row reaches the member through the checkpoint or not at all: the
		// broadcast it also rides is lost, which is the swallow the 60 s repair exists
		// for. The repair's set (one chunk, a newer revision) must still land.
		network.SetFaults(HostId, GuestId, new LinkFaults { Down = true });
		hostAuthority.ObserveSpawn(HostId, 88, "water", 5f, 6f);
		network.SetFaults(HostId, GuestId, new LinkFaults { Down = false });
		hostKernel.SendCheckpoint(GuestId);

		Assert.NotNull(guestAuthority.FindItem(88));
	}

	[Fact]
	public void Guest_SupersededSetOfTheSameRunDoesNotBlockTheLiveSet()
	{
		var (network, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];
		guest.Steam.FireLobbyEntered(LobbyId);

		var hostAuthority = host.Services.GetRequiredService<ItemKernelAuthority>();
		hostAuthority.ResetSessionState();
		hostAuthority.ObserveSpawn(HostId, 77, "water", 3f, 4f);
		host.Services.GetRequiredService<IWorldControl>().SendWorldJoin(isTutorial: false);
		var hostKernel = host.Services.GetRequiredService<IKernelProtocolControl>();
		var guestKernel = guest.Services.GetRequiredService<IKernelProtocolControl>();
		hostKernel.SendCheckpoint(GuestId);

		var guestAuthority = guest.Services.GetRequiredService<ItemKernelAuthority>();
		Assert.NotNull(guestAuthority.FindItem(77));
		var restoredRevision = guestAuthority.CurrentGlobalRevision;

		// The same run, a set that declares another chunk count: the live set
		// supersedes it, so the partial set is dropped as a unit instead of holding
		// a slot the live set needs. Buffered alone it restores nothing.
		guestKernel.HandleFrame(HostId, CheckpointFrame(TrailingChunkOfASupersededSet(runEpoch: 2, globalRevision: restoredRevision)));
		Assert.Equal(restoredRevision, guestAuthority.CurrentGlobalRevision);

		network.SetFaults(HostId, GuestId, new LinkFaults { Down = true });
		hostAuthority.ObserveSpawn(HostId, 88, "water", 5f, 6f);
		network.SetFaults(HostId, GuestId, new LinkFaults { Down = false });
		hostKernel.SendCheckpoint(GuestId);

		Assert.NotNull(guestAuthority.FindItem(88));
	}

	[Fact]
	public void Guest_RefusesCheckpointChunkWhoseHeaderDisagreesWithItsPayload()
	{
		var (_, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];
		guest.Steam.FireLobbyEntered(LobbyId);

		// The host is on run 2 while this guest process is still on 1, so an adopted
		// restore is visible as the epoch moving.
		var hostAuthority = host.Services.GetRequiredService<ItemKernelAuthority>();
		hostAuthority.ResetSessionState();
		hostAuthority.ObserveSpawn(HostId, 42, "water", 1f, 2f);

		// Both stamps come from the same checkpoint at send time, so a frame whose
		// two stamps disagree is malformed by construction and never restores.
		var frame = CheckpointFrame(WireCheckpointAssembler.Split(hostAuthority.CreateCheckpoint(), TestKernelCodec.Instance)[0]);
		frame.Checkpoint!.Header.RunEpoch = 9;

		guest.Services.GetRequiredService<IKernelProtocolControl>().HandleFrame(HostId, frame);

		Assert.Equal(1UL, guest.Services.GetRequiredService<ItemKernelAuthority>().CurrentRunEpoch.Value);
	}

	[Fact]
	public void Host_DropsGuestStateStreamWithStaleRunEpoch()
	{
		var (_, host, _) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];

		var hostKernel = host.Services.GetRequiredService<IKernelProtocolControl>();
		var received = new List<WireStateStream>();
		hostKernel.EntityStateStreamReceived += (_, _, stream) => received.Add(stream);

		hostKernel.HandleFrame(GuestId, new ProtocolFrame
		{
			Kind = EnvelopeKind.StateStream,
			StateStream = new StateStreamEnvelope
			{
				Header = new EnvelopeHeader
				{
					ProtocolVersion = ProtocolConstants.EnvelopeVersion,
					RunEpoch = 0, // a report from a previous run
					SenderId = GuestId,
					PayloadType = WirePayloadType.PlayerStateStream,
				},
				Stream = new WireStateStream
				{
					Seq = 1,
					PlayerStates =
					[
						new WirePlayerStreamState
						{
							EntityId = new WireEntityId { Epoch = 1, Counter = (uint)GuestId, Generation = 0 },
						},
					],
				},
			},
		});

		Assert.Empty(received);
	}

	[Fact]
	public void Host_DropsMalformedFrameWithMultipleEnvelopes()
	{
		var (_, host, _) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);

		var kernel = host.Services.GetRequiredService<IKernelProtocolControl>();
		var frame = SpawnCommandFrame();
		frame.CommittedBatch = new CommittedBatchEnvelope
		{
			Header = new EnvelopeHeader
			{
				ProtocolVersion = ProtocolConstants.EnvelopeVersion,
				RunEpoch = 1,
				SenderId = GuestId,
				OperationId = 1,
				PayloadType = WirePayloadType.CommittedBatch,
			},
			Batch = new WireCommittedBatch(),
		};

		kernel.HandleFrame(GuestId, frame);

		Assert.Null(host.Services.GetRequiredService<ItemKernelAuthority>().FindItem(42));
	}

	[Fact]
	public void Host_DropsCommandWithForgedSender()
	{
		var (_, host, _) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);

		var kernel = host.Services.GetRequiredService<IKernelProtocolControl>();
		var frame = SpawnCommandFrame();
		frame.Command!.Header.SenderId = 5555;

		kernel.HandleFrame(GuestId, frame);

		Assert.Null(host.Services.GetRequiredService<ItemKernelAuthority>().FindItem(42));
	}

	[Fact]
	public void Guest_DropsBatchWithMismatchedPayload()
	{
		var (_, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);

		var kernel = guest.Services.GetRequiredService<IKernelProtocolControl>();
		var frame = BatchFrame(SpawnWireBatch(globalRevision: 1, itemId: 42));
		frame.CommittedBatch!.Header.PayloadType = WirePayloadType.StateStream;

		kernel.HandleFrame(HostId, frame);

		Assert.Null(guest.Services.GetRequiredService<ItemKernelAuthority>().FindItem(42));
	}

	private static ProtocolFrame CheckpointFrame(WireCheckpoint chunk) =>
		new()
		{
			Kind = EnvelopeKind.Checkpoint,
			Checkpoint = new CheckpointEnvelope
			{
				Header = new EnvelopeHeader
				{
					ProtocolVersion = ProtocolConstants.EnvelopeVersion,
					RunEpoch = chunk.RunEpoch,
					SenderId = HostId,
					PayloadType = WirePayloadType.CheckpointChunk,
					BaseGlobalRevision = chunk.GlobalRevision,
				},
				Checkpoint = chunk,
			},
		};

	/// <summary>A trailing chunk of a two-chunk set: delivered alone it leaves the pending
	/// set incomplete, which is the shape a superseded set's leftover has.</summary>
	private static WireCheckpoint TrailingChunkOfASupersededSet(ulong runEpoch, ulong globalRevision) =>
		new()
		{
			ChunkIndex = 1,
			ChunkCount = 2,
			RunEpoch = runEpoch,
			GlobalRevision = globalRevision,
		};

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

	private static ProtocolFrame SnapshotFrame(WirePayloadType payloadType, uint seq, ulong baseRevision, WireWorldItemState item) =>
		new()
		{
			Kind = EnvelopeKind.StateStream,
			StateStream = new StateStreamEnvelope
			{
				Header = new EnvelopeHeader
				{
					ProtocolVersion = ProtocolConstants.EnvelopeVersion,
					RunEpoch = 1,
					SenderId = HostId,
					PayloadType = payloadType,
				},
				Stream = new WireStateStream
				{
					Seq = seq,
					BaseGlobalRevision = baseRevision,
					ItemStates = [item],
				},
			},
		};

	private static WireWorldItemState SnapshotItem(ulong id) =>
		new()
		{
			Identity = new WireItemIdentity { InstanceId = id, DefinitionId = "water" },
			Data = new WireItemData { Condition = 0.8f },
			X = 1f,
			Y = 2f,
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
