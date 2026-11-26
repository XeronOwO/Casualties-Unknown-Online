using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameState.Domains.Items;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using CasualtiesUnknownOnline.Application.Kernel;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// A run switch is announced to the members already in the world before the new
/// run's first committed batch leaves the host: their kernels still validate
/// against the previous run and would drop that batch as foreign-epoch.
/// </summary>
[Trait("Category", "Integration")]
public class KernelProtocolServiceRunAnnouncementTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	[Fact]
	public void Guest_AdoptsARebaselineCheckpointOfTheNextRun()
	{
		var (_, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];
		guest.Steam.FireLobbyEntered(LobbyId);

		var guestKernel = guest.Services.GetRequiredService<IKernelProtocolControl>();
		guestKernel.AdoptHostRunEpoch(1);

		ulong adopted = 0;
		guest.Services.GetRequiredService<IWorldControl>().EntryNamesAnotherRun += epoch => adopted = epoch;

		guestKernel.HandleFrame(HostId, KernelProtocolServiceTests.CheckpointFrame(new WireCheckpoint
		{
			ChunkIndex = 0,
			ChunkCount = 1,
			RunEpoch = 2,
			GlobalRevision = 1,
		}, rebaseline: true));

		Assert.Equal(2UL, guest.Services.GetRequiredService<ItemKernelAuthority>().CurrentRunEpoch.Value);
		Assert.Equal(2UL, adopted); // the adapter leaves the previous run's world on this edge
	}

	[Fact]
	public void Guest_StillRefusesACheckpointFromAnotherRunWithoutTheRebaselineMark()
	{
		var (_, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];
		guest.Steam.FireLobbyEntered(LobbyId);

		var guestKernel = guest.Services.GetRequiredService<IKernelProtocolControl>();
		guestKernel.AdoptHostRunEpoch(1);

		guestKernel.HandleFrame(HostId, KernelProtocolServiceTests.CheckpointFrame(new WireCheckpoint
		{
			ChunkIndex = 0,
			ChunkCount = 1,
			RunEpoch = 2,
			GlobalRevision = 1,
		}));

		Assert.Equal(1UL, guest.Services.GetRequiredService<ItemKernelAuthority>().CurrentRunEpoch.Value);
	}

	[Fact]
	public void Host_SendsTheNextRunsBaselineBeforeItsFirstBatch()
	{
		var (_, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];
		guest.Steam.FireLobbyEntered(LobbyId);

		// The member is told to serve run 1, as a real entry instruction would.
		host.Services.GetRequiredService<PacketSender>().Send(GuestId, NetMsg.WorldJoin, new WorldJoinMsg { IsTutorial = false, RunEpoch = 1 });

		// The member is already in the world: its kernel still serves run 1, so a
		// run-2 batch would be dropped as foreign-epoch without the announcement.
		guest.Services.GetRequiredService<ISessionControl>().ReportSceneState(SceneStateType.InWorld, "level1", new NetVector2(1f, 2f));

		var hostAuthority = host.Services.GetRequiredService<ItemKernelAuthority>();
		hostAuthority.ObserveSpawn(HostId, 42, "water", 1f, 2f); // run 1's batch
		var guestAuthority = guest.Services.GetRequiredService<ItemKernelAuthority>();
		Assert.Equal(1UL, guestAuthority.CurrentRunEpoch.Value);

		var received = new List<ProtocolFrame>();
		guest.Transport.MessageReceived += (_, frame) =>
		{
			if ((NetMsg)frame[0] == NetMsg.KernelEnvelope)
			{
				received.Add(NetPacket.DecodePayload<ProtocolFrame>(frame));
			}
		};

		hostAuthority.ResetSessionState(); // the host moves to run 2
		hostAuthority.ObserveSpawn(HostId, 77, "water", 3f, 4f); // run 2's first batch

		var rebaselineAt = received.FindIndex(frame => frame.Checkpoint?.Rebaseline == true);
		var batchAt = received.FindIndex(frame => frame.CommittedBatch is not null);
		Assert.True(rebaselineAt >= 0, "the run switch must send a re-baseline checkpoint");
		Assert.True(batchAt > rebaselineAt, $"the re-baseline must precede the batch (re-baseline {rebaselineAt}, batch {batchAt})");

		// Without the host's re-baseline the member would drop the run-2 batch as
		// foreign-epoch (it still serves run 1) and never restore the batch's rows.
		Assert.Equal(2UL, guestAuthority.CurrentRunEpoch.Value);
		Assert.NotNull(guestAuthority.FindItem(77));
	}

	[Fact]
	public void Guest_KeepsTheEpochGuardStrictWhileEnteringANewerRun()
	{
		var (_, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, GuestId];
		guest.Steam.FireLobbyEntered(LobbyId);

		var guestKernel = guest.Services.GetRequiredService<IKernelProtocolControl>();
		var guestAuthority = guest.Services.GetRequiredService<ItemKernelAuthority>();

		// The host's first run-2 batch can arrive before the run-2 announcement (the
		// two travel on different paths; 14 ms apart on 2026-11-22). It must not be
		// applied while this side still serves run 1: the entry checkpoint carries
		// the new run's baseline.
		guestKernel.HandleFrame(HostId, KernelProtocolServiceTests.BatchFrame(
			KernelProtocolServiceTests.SpawnWireBatch(globalRevision: 1, runEpoch: 2, itemId: 43)));

		Assert.Null(guestAuthority.FindItem(43));
	}
}
