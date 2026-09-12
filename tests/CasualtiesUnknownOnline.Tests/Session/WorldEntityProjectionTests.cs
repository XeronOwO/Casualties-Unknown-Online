using System.Collections.Generic;
using System.Linq;
using CasualtiesUnknownOnline.GameState;
using CasualtiesUnknownOnline.GameState.Domains.Items;
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
	public void GuestCheckpointRestore_SkipsTransientRepeatableTrapStates()
	{
		var (_, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, 2001];
		guest.Steam.FireLobbyEntered(LobbyId);

		var hostWorld = host.Services.GetRequiredService<IWorldControl>();
		var hostAuthority = host.Services.GetRequiredService<ItemKernelAuthority>();
		hostWorld.ReportTrapState(EntityEventKind.TurretFired, 1.2f, 2.8f, 0);
		hostWorld.ReportTrapState(EntityEventKind.GeyserActivated, 3.4f, 4.6f, 0);
		hostWorld.ReportTrapState(EntityEventKind.BearTrapClamped, 7.2f, 8.8f, 3);

		var projection = guest.Services.GetRequiredService<WorldEntityKernelProjection>();
		IReadOnlyList<EntityEventMsg>? traps = null;
		projection.TrapSnapshotProjected += list => traps = list;

		var guestAuthority = guest.Services.GetRequiredService<ItemKernelAuthority>();
		Assert.True(guestAuthority.Restore(hostAuthority.CreateCheckpoint()).Success);

		Assert.NotNull(traps);
		Assert.DoesNotContain(traps!, t => t.Kind == EntityEventKind.TurretFired);
		Assert.DoesNotContain(traps!, t => t.Kind == EntityEventKind.GeyserActivated);
		Assert.Contains(traps!, t => t.Kind == EntityEventKind.BearTrapClamped);
	}

	[Fact]
	public void GuestCheckpointRestore_FiltersPreExistingTransientTrapStateFacts()
	{
		var (_, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, 2001];
		guest.Steam.FireLobbyEntered(LobbyId);

		// Seed old transient facts directly into the kernel state table. This
		// simulates a checkpoint created before the classification fix: the
		// projection must filter them even when they already exist.
		var hostAuthority = host.Services.GetRequiredService<ItemKernelAuthority>();
		Assert.True(hostAuthority.TryExecuteCommand(
			new RecordTrapStateCommand(
				new OperationId(1),
				new ActorId(HostId),
				new RunEpoch(1),
				AuthorityKind.HostOnly,
				new EntityPosition(1, 2),
				(int)EntityEventKind.TurretFired,
				TrapPhase.Triggered,
				0,
				100),
			HostId,
			out _,
			out _));
		Assert.True(hostAuthority.TryExecuteCommand(
			new RecordTrapStateCommand(
				new OperationId(2),
				new ActorId(HostId),
				new RunEpoch(1),
				AuthorityKind.HostOnly,
				new EntityPosition(3, 4),
				(int)EntityEventKind.GeyserActivated,
				TrapPhase.Triggered,
				0,
				200),
			HostId,
			out _,
			out _));

		var projection = guest.Services.GetRequiredService<WorldEntityKernelProjection>();
		IReadOnlyList<EntityEventMsg>? traps = null;
		projection.TrapSnapshotProjected += list => traps = list;

		var guestAuthority = guest.Services.GetRequiredService<ItemKernelAuthority>();
		Assert.True(guestAuthority.Restore(hostAuthority.CreateCheckpoint()).Success);

		// The projection only raises when at least one non-skipped state exists,
		// so null is the correct "no transient facts projected" result here.
		Assert.True(traps is null || traps.Count == 0, "transient turret/geyser state must not reach the checkpoint projection");
		Assert.DoesNotContain(traps ?? [], t => t.Kind == EntityEventKind.TurretFired);
		Assert.DoesNotContain(traps ?? [], t => t.Kind == EntityEventKind.GeyserActivated);
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
	public void HostReportTrapEvent_WithDrops_CommitsTrapAndItemSpawnsInOneBatch()
	{
		var (_, host, _) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);

		var world = host.Services.GetRequiredService<IWorldControl>();
		var authority = host.Services.GetRequiredService<ItemKernelAuthority>();
		var items = host.Services.GetRequiredService<IItemControl>();

		CommittedBatch? captured = null;
		authority.BatchCommitted += batch => captured = batch;
		WorldItem? projected = null;
		items.ItemSpawned += w => projected = w;

		world.ReportTrapEvent(
			EntityEventKind.MineExploded,
			1.2f,
			2.8f,
			5,
			0f,
			null,
			[
				new TrapDropEntryMsg
				{
					ItemId = 501,
					Item = new CharacterItemMsg { ItemId = "rock", Condition = 1f },
					Position = new NetVector2Msg(1.2f, 2.8f),
				},
			],
			dropActor: 2001);

		Assert.NotNull(captured);
		Assert.Contains(captured!.Events, e => e is TrapConsumedEvent);
		Assert.Contains(captured.Events, e => e is TrapStateChangedEvent);
		Assert.Contains(captured.Events, e => e is ItemSpawnedEvent);
		Assert.Single(authority.QueryItems().Values.Where(i => i.Identity.InstanceId == 501));
		Assert.NotNull(projected);
		Assert.Equal(501ul, projected!.Value.ItemId);
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

	[Fact]
	public void HostCheckpointRestore_ArmsTheWorldEntryWriteWithTheFactsTheGuestProjects()
	{
		// The SAME restored fact table has two landing moments. A guest applies the
		// checkpoint to the world it already stands in, so the projection raises its
		// lists now; the host restores BEFORE the scene load, so the only world alive is
		// the layer being replaced and the facts wait for the world-entry seam. Both
		// paths must produce the same rows — there is one mapping, not two.
		var (_, host, guest) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		host.Steam.LobbyMembers = [HostId, 2001];
		guest.Steam.FireLobbyEntered(LobbyId);

		var hostWorld = host.Services.GetRequiredService<IWorldControl>();
		hostWorld.ReportTrapConsumed(EntityEventKind.MineExploded, 1.2f, 2.8f, 5);
		hostWorld.ReportOpenedEntity(3.1f, 4.2f);
		hostWorld.ReportBuildingEntityHealth(5.1f, 6.2f, 7.5f);
		var checkpoint = host.Services.GetRequiredService<ItemKernelAuthority>().CreateCheckpoint();

		var guestProjection = guest.Services.GetRequiredService<WorldEntityKernelProjection>();
		IReadOnlyList<EntityEventMsg>? guestTraps = null;
		IReadOnlyList<NetVector2Msg>? guestOpened = null;
		IReadOnlyList<BuildingEntityHealthEntryMsg>? guestHealth = null;
		guestProjection.TrapSnapshotProjected += list => guestTraps = list;
		guestProjection.OpenedEntitiesProjected += list => guestOpened = list;
		guestProjection.BuildingHealthProjected += list => guestHealth = list;
		Assert.True(guest.Services.GetRequiredService<ItemKernelAuthority>().Restore(checkpoint).Success);

		var hostProjection = host.Services.GetRequiredService<WorldEntityKernelProjection>();
		var raised = 0;
		hostProjection.TrapSnapshotProjected += _ => raised++;
		hostProjection.OpenedEntitiesProjected += _ => raised++;
		hostProjection.BuildingHealthProjected += _ => raised++;
		Assert.True(host.Services.GetRequiredService<ItemKernelAuthority>().Restore(checkpoint).Success);

		Assert.Equal(0, raised);
		var source = (IRestoredWorldEntitySource)hostProjection;
		Assert.True(source.HasPendingRestore);
		var pending = source.ReadPendingFacts();
		Assert.Equal(3, pending.Count);
		Assert.Equal(5, Assert.Single(pending.Traps).Extra);
		Assert.Equal(Assert.Single(guestTraps!).Extra, Assert.Single(pending.Traps).Extra);
		Assert.Equal(Assert.Single(guestOpened!).X, Assert.Single(pending.Opened).X);
		Assert.Equal(Assert.Single(guestOpened!).Y, Assert.Single(pending.Opened).Y);
		Assert.Equal(Assert.Single(guestHealth!).X, Assert.Single(pending.Health).X);
		Assert.Equal(Assert.Single(guestHealth!).Health, Assert.Single(pending.Health).Health);
	}

	[Fact]
	public void SoloCheckpointRestore_ArmsTheWorldEntryWriteToo()
	{
		// Solo play has no session role at all (SessionRole.None), and it restores the
		// same way a host does: the gate is "guest projects now, everyone else waits",
		// never "the host". The registry's live Report path is host-only, so the fact is
		// seeded straight into the kernel, exactly as the run that produced it would.
		var (_, solo, _) = HandshakeTests.CreateHostAndGuest();
		var soloAuthority = solo.Services.GetRequiredService<ItemKernelAuthority>();
		Assert.True(soloAuthority.TryExecuteCommand(
			new RecordOpenedEntityCommand(
				new OperationId(1),
				new ActorId(HostId),
				new RunEpoch(1),
				AuthorityKind.HostOnly,
				new EntityPosition(3, 4)),
			HostId,
			out _,
			out _));
		var checkpoint = soloAuthority.CreateCheckpoint();

		var projection = solo.Services.GetRequiredService<WorldEntityKernelProjection>();
		var raised = 0;
		projection.OpenedEntitiesProjected += _ => raised++;
		Assert.True(soloAuthority.Restore(checkpoint).Success);

		Assert.Equal(0, raised);
		var source = (IRestoredWorldEntitySource)projection;
		Assert.True(source.HasPendingRestore);
		Assert.Equal(3.5f, Assert.Single(source.ReadPendingFacts().Opened).X);
	}

	[Fact]
	public void PendingWorldEntryWrite_EndsOnCommitOrCancel_AndNeverTwice()
	{
		var (_, host, _) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		var world = host.Services.GetRequiredService<IWorldControl>();
		var authority = host.Services.GetRequiredService<ItemKernelAuthority>();
		world.ReportOpenedEntity(3.1f, 4.2f);
		var checkpoint = authority.CreateCheckpoint();

		var projection = host.Services.GetRequiredService<WorldEntityKernelProjection>();
		var source = (IRestoredWorldEntitySource)projection;

		// Armed by a restore that has not reached the world-entry seam yet.
		Assert.True(authority.Restore(checkpoint).Success);
		Assert.True(source.HasPendingRestore);

		// The live world took every row.
		source.CommitPendingRestore();
		Assert.False(source.HasPendingRestore);

		// A layer-end restore's facts describe the layer being replaced: the arm is
		// cancelled (and logged), and a second cancel is a no-op rather than a throw.
		Assert.True(authority.Restore(checkpoint).Success);
		Assert.True(source.HasPendingRestore);
		source.CancelPendingRestore("the cut is a layer-end cut: its world-entity facts describe the layer being replaced");
		Assert.False(source.HasPendingRestore);
		source.CancelPendingRestore("nothing is armed");
		Assert.False(source.HasPendingRestore);
	}

	[Fact]
	public void SessionEnd_ReleasesThePendingWorldEntryWrite()
	{
		// A session end takes the layer the arm belongs to with it. Left armed, the next
		// generation would skip its layer-boundary reset (the seam treats a pending
		// restore as "keep the tables") and write a previous session's world-entity facts
		// into a new world.
		var (_, host, _) = HandshakeTests.CreateHostAndGuest();
		host.Steam.FireLobbyCreated(LobbyId);
		var world = host.Services.GetRequiredService<IWorldControl>();
		var authority = host.Services.GetRequiredService<ItemKernelAuthority>();
		// Resolved before the restore, like every production composition does: the
		// projection subscribes to the kernel's CheckpointRestored in its constructor, so
		// a first resolve AFTER a restore would never see that restore.
		var projection = host.Services.GetRequiredService<WorldEntityKernelProjection>();
		world.ReportOpenedEntity(3.1f, 4.2f);
		Assert.True(authority.Restore(authority.CreateCheckpoint()).Success);

		var source = (IRestoredWorldEntitySource)projection;
		Assert.True(source.HasPendingRestore);

		host.Session.EndSession();

		Assert.False(source.HasPendingRestore);
	}
}
