using System.Collections.Generic;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.WorldEntities;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Session;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

public class WorldEntityProjectionTests
{
	private const ulong HostId = 1001;
	private const ulong LobbyId = 9001;

	[Fact]
	public void HostReports_CommitKernelWorldEntities()
	{
		var (_, host, _) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);

		var world = host.Services.GetRequiredService<IWorldControl>();
		var authority = host.Services.GetRequiredService<ItemKernelAuthority>();

		world.ReportTrapConsumed(EntityEventKind.MineExploded, 1.2f, 2.8f, 5);
		world.ReportOpenedEntity(3.1f, 4.2f);
		world.ReportBuildingEntityHealth(5.1f, 6.2f, 7.5f);

		var state = authority.QueryWorldEntities();
		Assert.NotNull(state);
		var trap = Assert.Single(state!.Consumptions);
		Assert.Equal(1, trap.Position.X);
		Assert.Equal(2, trap.Position.Y);
		Assert.Equal((int)EntityEventKind.MineExploded, trap.Kind);
		Assert.Equal(5, trap.Extra);

		var opened = Assert.Single(state.OpenedEntities);
		Assert.Equal(new EntityPosition(3, 4), opened.Position);

		var health = Assert.Single(state.BuildingHealth);
		Assert.Equal(new EntityPosition(5, 6), health.Position);
		Assert.Equal(7.5f, health.Health);
	}

	[Fact]
	public void GuestCheckpointRestore_ProjectsNonOneShotTrapStateFacts()
	{
		var (_, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, 2001];
		guest.Steam.FireLobbyEntered(LobbyId);

		var hostWorld = host.Services.GetRequiredService<IWorldControl>();
		var hostAuthority = host.Services.GetRequiredService<ItemKernelAuthority>();
		hostWorld.ReportTrapState(EntityEventKind.BearTrapClamped, 7.2f, 8.8f, 3);

		var projection = guest.Services.GetRequiredService<WorldEntityKernelProjection>();
		IReadOnlyList<EntityEventMsg>? traps = null;
		projection.TrapSnapshotProjected += list => traps = list;

		var guestAuthority = guest.Services.GetRequiredService<ItemKernelAuthority>();
		Assert.True(guestAuthority.Restore(hostAuthority.CreateCheckpoint()).Success);

		Assert.NotNull(traps);
		var trap = Assert.Single(traps!);
		Assert.Equal(EntityEventKind.BearTrapClamped, trap.Kind);
		Assert.Equal(3, trap.Extra);
		Assert.Equal(7.5f, trap.Position.X);
		Assert.Equal(8.5f, trap.Position.Y);
	}

	[Fact]
	public void HostReports_CommitKernelTrapStateFacts()
	{
		var (_, host, _) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);

		var world = host.Services.GetRequiredService<IWorldControl>();
		var authority = host.Services.GetRequiredService<ItemKernelAuthority>();

		world.ReportTrapState(EntityEventKind.MinePressed, 1.2f, 2.8f, 0);
		world.ReportTrapState(EntityEventKind.MineExploded, 1.2f, 2.8f, 0);

		var state = authority.QueryWorldEntities();
		Assert.NotNull(state);
		Assert.Equal(2, state!.TrapStates.Count);
		Assert.Contains(state.TrapStates, s => s.Position == new EntityPosition(1, 2)
			&& s.Kind == (int)EntityEventKind.MinePressed
			&& s.Phase == TrapPhase.Warning);
		Assert.Contains(state.TrapStates, s => s.Position == new EntityPosition(1, 2)
			&& s.Kind == (int)EntityEventKind.MineExploded
			&& s.Phase == TrapPhase.Triggered);
	}

	[Fact]
	public void HostReportTrapEvent_CommitsOneAtomicKernelBatch()
	{
		var (_, host, _) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);

		var world = host.Services.GetRequiredService<IWorldControl>();
		var authority = host.Services.GetRequiredService<ItemKernelAuthority>();

		CommittedBatch? captured = null;
		authority.BatchCommitted += batch => captured = batch;

		world.ReportTrapEvent(
			EntityEventKind.MineExploded,
			1.2f,
			2.8f,
			5,
			0f,
			[
				new BuildingEntityHealthEntryMsg { X = 3.4f, Y = 6.8f, Health = 7f },
			]);

		Assert.NotNull(captured);
		Assert.Equal(4, captured!.Events.Count);
		Assert.Contains(captured.Events, e => e is TrapConsumedEvent);
		Assert.Contains(captured.Events, e => e is TrapStateChangedEvent);
		Assert.Contains(captured.Events, e => e is BuildingEntityHealthUpdatedEvent);

		var state = authority.QueryWorldEntities();
		Assert.NotNull(state);
		Assert.Single(state!.Consumptions);
		Assert.Single(state.TrapStates);
		Assert.Equal(2, state.BuildingHealth.Count);
	}

	[Fact]
	public void GuestCheckpointRestore_ProjectsKernelWorldEntities()
	{
		var (_, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, 2001];
		guest.Steam.FireLobbyEntered(LobbyId);

		var hostWorld = host.Services.GetRequiredService<IWorldControl>();
		var hostAuthority = host.Services.GetRequiredService<ItemKernelAuthority>();
		hostWorld.ReportTrapConsumed(EntityEventKind.MineExploded, 1.2f, 2.8f, 5);
		hostWorld.ReportOpenedEntity(3.1f, 4.2f);
		hostWorld.ReportBuildingEntityHealth(5.1f, 6.2f, 7.5f);

		var projection = guest.Services.GetRequiredService<WorldEntityKernelProjection>();
		IReadOnlyList<EntityEventMsg>? traps = null;
		IReadOnlyList<NetVector2Msg>? opened = null;
		IReadOnlyList<BuildingEntityHealthEntryMsg>? health = null;
		projection.TrapSnapshotProjected += list => traps = list;
		projection.OpenedEntitiesProjected += list => opened = list;
		projection.BuildingHealthProjected += list => health = list;

		var guestAuthority = guest.Services.GetRequiredService<ItemKernelAuthority>();
		Assert.True(guestAuthority.Restore(hostAuthority.CreateCheckpoint()).Success);

		Assert.NotNull(traps);
		Assert.Equal(5, Assert.Single(traps!).Extra);
		Assert.Equal(3.5f, Assert.Single(opened!).X);
		Assert.Equal(4.5f, Assert.Single(opened!).Y);
		Assert.Equal(6.5f, Assert.Single(health!).Y);
		Assert.Equal(7.5f, Assert.Single(health!).Health);
	}
}
