using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Runtime.Time;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// Phase-2 entity-event simulations over a THREE-node star: the fixed,
/// kind-specific deep scenarios (the relay topology, the one-shot
/// consumption facts, the snapshot lifecycle, the spawn relay and the fluid
/// channel's unreliable absolute-region semantics). The world construction
/// and the host-executor shell are shared (EntityEventSimWorld); the
/// phase-5 combinatorial suite (EntityEventBehaviorTests) covers every kind
/// against the generic scenario families.
/// </summary>
public class EntityEventSimulationTests
{
	[Fact]
	public void GuestTrigger_RelayedToOtherGuest_SourceExcluded()
	{
		var w = EntityEventSimWorld.Create();
		w.Trigger(w.G1, EntityEventKind.MineExploded, 10f, 20f);

		Assert.True(w.G2Events.Count == 1,
			$"the other guest must get exactly one copy, got {w.G2Events.Count} (host executed {w.HostExecutions.Value} time(s))");
		Assert.True(w.G2Events[0].Kind == EntityEventKind.MineExploded, "the relay carries the event");
		Assert.True(w.G2Events[0].Position.X == 10f && w.G2Events[0].Position.Y == 20f, "the position key rides through");
		Assert.Empty(w.G1Events);
	}

	[Fact]
	public void HostTrigger_BroadcastToEveryGuest()
	{
		var w = EntityEventSimWorld.Create();
		w.Trigger(w.Host, EntityEventKind.SpikeStabbed, 5f, 5f);

		Assert.True(w.G1Events.Count == 1 && w.G2Events.Count == 1, $"both guests must get one copy (g1: {w.G1Events.Count}, g2: {w.G2Events.Count})");
	}

	[Fact]
	public void DuplicateReport_GuardDropsTheSecondExecution_ConsumptionStaysOne()
	{
		var w = EntityEventSimWorld.Create();
		w.Trigger(w.G1, EntityEventKind.MineExploded, 10f, 20f);
		w.Trigger(w.G1, EntityEventKind.MineExploded, 10f, 20f); // a retransmit

		// The handler relays unconditionally (the message layer is not the guard —
		// the relayed duplicate is what the guests' own replay guards consume).
		Assert.True(w.G2Events.Count == 2, $"both reports relay (the replay guard lives on the receiving side), got {w.G2Events.Count}");
		Assert.True(w.HostExecutions.Value == 1, $"the HOST executes the consumption once, got {w.HostExecutions.Value}");
	}

	[Fact]
	public void OneShotConsumption_SnapshotCarriesTheLatest()
	{
		var w = EntityEventSimWorld.Create();
		var g1Consumed = new List<IReadOnlyList<EntityEventMsg>>();
		w.G1.Services.GetRequiredService<EntityEventChannel>().TrapStateReceived += list => g1Consumed.Add(list);

		// The same one-shot entity progresses (ScrapEaterProgress carries the %).
		w.HostChannel.ReportTrapConsumed(EntityEventKind.ScrapEaterProgress, 30f, 40f, extra: 25);
		w.HostChannel.ReportTrapConsumed(EntityEventKind.ScrapEaterProgress, 30f, 40f, extra: 50); // overwrites
		w.HostChannel.SendTrapStateSnapshot(w.G1.SteamId);

		Assert.True(g1Consumed.Count == 1, "the snapshot must arrive");
		Assert.True(g1Consumed[0].Count == 1, $"one consumed entity, got {g1Consumed[0].Count}");
		Assert.True(g1Consumed[0][0].Kind == EntityEventKind.ScrapEaterProgress && g1Consumed[0][0].Extra == 50,
			"the latest consumption (progress 50) is what the late joiner replays");
	}

	[Fact]
	public void OneShotConsumption_ResetClears_NewWorldStartsEmpty()
	{
		var w = EntityEventSimWorld.Create();
		w.Trigger(w.G1, EntityEventKind.MineExploded, 10f, 20f);
		w.HostChannel.ResetConsumptions(); // a new layer is generating

		var g2Consumed = new List<IReadOnlyList<EntityEventMsg>>();
		w.G2.Services.GetRequiredService<EntityEventChannel>().TrapStateReceived += list => g2Consumed.Add(list);
		w.HostChannel.SendTrapStateSnapshot(w.G2.SteamId);

		Assert.True(g2Consumed.Count == 0, "an empty consumption table sends nothing");
	}

	[Fact]
	public void TrapStateSnapshot_LateJoinerConsumesEveryEntry()
	{
		var w = EntityEventSimWorld.Create();
		w.HostChannel.ReportTrapConsumed(EntityEventKind.MineExploded, 10f, 20f, extra: 0);
		w.HostChannel.ReportTrapConsumed(EntityEventKind.BioTerminalUnlocked, 30f, 40f, extra: 0);

		w.HostChannel.SendTrapStateSnapshot(w.G1.SteamId);

		// The late joiner replays every entry against its regenerated world
		// (the snapshot-consumption step) — two consumed entities, two replays.
		Assert.True(w.G1Replays.Value == 2, $"the late joiner must consume every entry, got {w.G1Replays.Value}");
	}

	[Fact]
	public void TrapStateSnapshot_DuplicateSnapshot_ConsumesOnce()
	{
		var w = EntityEventSimWorld.Create();
		w.HostChannel.ReportTrapConsumed(EntityEventKind.MineExploded, 10f, 20f, extra: 0);

		w.HostChannel.SendTrapStateSnapshot(w.G1.SteamId);
		w.HostChannel.SendTrapStateSnapshot(w.G1.SteamId); // the 60 s re-send

		Assert.True(w.G1Replays.Value == 1, $"a duplicate snapshot must not re-consume, got {w.G1Replays.Value}");
	}

	[Fact]
	public void OpenedEntity_SnapshotCarriesEveryDistinctPosition()
	{
		var w = EntityEventSimWorld.Create();
		var g1Opened = new List<IReadOnlyList<NetVector2Msg>>();
		w.G1.Services.GetRequiredService<EntityEventChannel>().OpenedEntitiesSnapshotReceived += list => g1Opened.Add(list);

		w.HostChannel.ReportOpenedEntity(10.2f, 20.8f);
		w.HostChannel.ReportOpenedEntity(30f, 40f);
		w.HostChannel.ReportOpenedEntity(10.7f, 20.1f); // the same cell — idempotent
		w.HostChannel.SendOpenedEntitiesSnapshot(w.G1.SteamId);

		Assert.True(g1Opened.Count == 1, "the snapshot must arrive");
		Assert.True(g1Opened[0].Count == 2, $"two distinct cells, got {g1Opened[0].Count}");
	}

	[Fact]
	public void OpenedEntity_ResetClears_NewWorldStartsEmpty()
	{
		var w = EntityEventSimWorld.Create();
		w.HostChannel.ReportOpenedEntity(10f, 20f);
		w.HostChannel.ResetOpenedEntities(); // a new layer is generating

		var g2Opened = new List<IReadOnlyList<NetVector2Msg>>();
		w.G2.Services.GetRequiredService<EntityEventChannel>().OpenedEntitiesSnapshotReceived += list => g2Opened.Add(list);
		w.HostChannel.SendOpenedEntitiesSnapshot(w.G2.SteamId);

		Assert.True(g2Opened.Count == 0, "an empty opened table sends nothing");
	}

	[Fact]
	public void Snapshot_ElapsedCarriesTheTriggerAge()
	{
		// The rejoin scenario (user-verified): the host's shuttle door opened,
		// MINUTES pass, the guest rejoins — the snapshot must carry how long
		// ago, so the door's replay lands at the CURRENT state (already open /
		// gone) instead of re-running the 10 s opening animation from zero.
		var w = EntityEventSimWorld.Create();
		var clock = (FakeClock)w.Host.Services.GetRequiredService<ITimeSource>();
		w.HostChannel.ReportTrapConsumed(EntityEventKind.ShuttleDoorOpened, 0f, 496f, extra: 0);
		clock.Advance(7_500); // 7.5 s later — the door's animation is mid-flight

		var g1Consumed = new List<IReadOnlyList<EntityEventMsg>>();
		w.G1.Services.GetRequiredService<EntityEventChannel>().TrapStateReceived += list => g1Consumed.Add(list);
		w.HostChannel.SendTrapStateSnapshot(w.G1.SteamId);

		Assert.True(g1Consumed.Count == 1, "the snapshot must arrive");
		Assert.True(g1Consumed[0][0].ElapsedSeconds > 7.4f && g1Consumed[0][0].ElapsedSeconds < 7.6f,
			$"the elapsed must ride the snapshot (7.5 s), got {g1Consumed[0][0].ElapsedSeconds}");
	}

	[Fact]
	public void Snapshot_ElapsedZero_ForLiveEvents()
	{
		// A live event's ElapsedSeconds is 0 (the transition just happened) —
		// the replay runs the full transition, the original behaviour.
		var w = EntityEventSimWorld.Create();
		w.Trigger(w.G1, EntityEventKind.SpikeStabbed, 10f, 20f);

		Assert.True(w.G2Events.Count == 1, "the relay must arrive");
		Assert.True(w.G2Events[0].ElapsedSeconds == 0f, $"a live event carries no elapsed, got {w.G2Events[0].ElapsedSeconds}");
	}

	[Fact]
	public void EntitySpawned_DomainRelaysOnce_ToEveryMember()
	{
		var w = EntityEventSimWorld.Create();
		var g1Spawns = new List<EntitySpawnedMsg>();
		var g2Spawns = new List<EntitySpawnedMsg>();
		w.G1.Services.GetRequiredService<IWorldControl>().EntitySpawnedReceived += (_, msg) => g1Spawns.Add(msg);
		w.G2.Services.GetRequiredService<IWorldControl>().EntitySpawnedReceived += (_, msg) => g2Spawns.Add(msg);

		w.G1.Services.GetRequiredService<IWorldControl>().SendEntitySpawned(new EntitySpawnedMsg
		{
			Id = "caveticknest",
			Position = new NetVector2Msg(7f, 8f),
		});

		// The ADAPTER domain is the single relay owner (the handler never
		// broadcasts). The host's relay is a broadcast to every member — the
		// source included, whose copy makes the repeat a no-op.
		Assert.True(g1Spawns.Count == 1 && g2Spawns.Count == 1,
			$"every member must get exactly one relay (g1: {g1Spawns.Count}, g2: {g2Spawns.Count})");
	}

	[Fact]
	public void CrystalMimicTriggered_HostTrigger_IsRecordedForTheLateJoiner()
	{
		var w = EntityEventSimWorld.Create();
		var consumed = new List<IReadOnlyList<EntityEventMsg>>();
		w.G1.Services.GetRequiredService<EntityEventChannel>().TrapStateReceived += list => consumed.Add(list);

		// A HOST trigger never comes back through EntityEventHandler (the host
		// is not in its own presence table) — the channel must record the
		// one-shot consumption before broadcasting, or a late joiner re-arms
		// the mimic and spawns a second crystalenemy set.
		w.Trigger(w.Host, EntityEventKind.CrystalMimicTriggered, 10f, 20f);
		w.HostChannel.SendTrapStateSnapshot(w.G1.SteamId);

		Assert.True(consumed.Count == 1 && consumed[0].Count == 1,
			$"the host-triggered mimic consumption must reach the snapshot (snapshots: {consumed.Count})");
		Assert.True(consumed[0][0].Kind == EntityEventKind.CrystalMimicTriggered,
			"the snapshot carries the mimic's one-shot consumption");
	}

	[Fact]
	public void CrystalMimicTriggered_EventAndSpawnsRideTheirOwnChannels()
	{
		var w = EntityEventSimWorld.Create();
		var g2Spawns = new List<EntitySpawnedMsg>();
		w.G2.Services.GetRequiredService<IWorldControl>().EntitySpawnedReceived += (_, msg) => g2Spawns.Add(msg);

		// One operation = one message per channel: the latch travels as the
		// dedicated entity event; the crystalenemy copies ride EntitySpawned
		// (the game spawns them inside Touched/Hit, CrystalMimic.cs:30-32).
		w.Trigger(w.G1, EntityEventKind.CrystalMimicTriggered, 10f, 20f);
		w.G1.Services.GetRequiredService<IWorldControl>().SendEntitySpawned(new EntitySpawnedMsg
		{
			Id = "crystalenemy",
			Position = new NetVector2Msg(10f, 20f),
		});

		Assert.True(w.G2Events.Count == 1 && w.G2Events[0].Kind == EntityEventKind.CrystalMimicTriggered,
			$"the other guest must get the mimic event, got {w.G2Events.Count}");
		Assert.True(g2Spawns.Count == 1 && g2Spawns[0].Id == "crystalenemy",
			$"the crystalenemy spawn must ride EntitySpawned, got {g2Spawns.Count}");
	}

	[Fact]
	public void FluidRegion_LostUnreliableRegion_HealedByTheNextAbsoluteOverwrite()
	{
		var w = EntityEventSimWorld.Create();
		var regions = new List<FluidRegionMsg>();
		w.G1.Services.GetRequiredService<EntityEventChannel>().FluidRegionReceived += msg => regions.Add(msg);
		w.Driver.Network.SetFaults(w.Host.SteamId, w.G1.SteamId, new LinkFaults { UnreliableDropRate = 0.5 }); // the unreliable stream loses ~half

		for (byte seq = 1; seq <= 10; seq++)
		{
			w.HostChannel.SendFluidRegion(w.G1.SteamId, new FluidRegionMsg { Seq = seq, OriginX = 0, OriginY = 0, Width = 4, Height = 1, Cells = [seq, 4] });
		}

		// Whatever the loss pattern, the ABSOLUTE-overwrite semantics converge:
		// the last ARRIVED region is the applied state — a lost one is healed by
		// the next (regions.Cells[0] == the value for the whole row = seq).
		Assert.True(regions.Count >= 1, "at least one region survives the loss");
		var last = regions[regions.Count - 1];
		Assert.True(last.Cells.Length == 2 && last.Cells[0] == last.Seq,
			$"the applied state is the LAST overwrite's (seq {last.Seq}, first run {(last.Cells.Length > 0 ? last.Cells[0] : -1)})");
	}

	[Fact]
	public void MineExplosion_CrossDomainMessageStorm_ReachesTheGuest()
	{
		// The most complex interaction in the game: ONE mine explosion rides
		// FOUR sync channels at once. The shell replays the host executor's
		// side-effect surface (TrapEffectApplier.ApplyMineExplosion's shape):
		// the crater (SetBlock → the BlockPlaced channel), the drops (the item
		// domain registers + relays) and the EntityEvent itself — every
		// consequence must reach the other guest, source excluded.
		var w = EntityEventSimWorld.Create();
		var g2Blocks = new List<(int X, int Y, ushort Block)>();
		var g2Drops = new List<ulong>();
		w.G2.Services.GetRequiredService<IWorldControl>().BlockPlacedReceived += (_, x, y, block) => g2Blocks.Add((x, y, block));
		w.G2.Transport.MessageReceived += (_, frame) =>
		{
			if ((NetMsg)frame[0] == NetMsg.ItemSpawn)
			{
				g2Drops.Add(NetPacket.DecodePayload<ItemSpawnMsg>(frame).ItemId);
			}
		};

		w.HostExecuted += msg =>
		{
			if (msg.Kind != EntityEventKind.MineExploded)
			{
				return;
			}

			// The explosion's side effects, exactly the channels the production
			// executor's consequences ride.
			var world = w.Host.Services.GetRequiredService<IWorldControl>();
			world.BroadcastBlockPlaced(w.Host.SteamId, 10, 11, 42); // the crater (SetBlock consequence)
			w.Host.Services.GetRequiredService<ItemService>().SendItemSpawned(
				500, new CharacterItemMsg { ItemId = "dropped_ore", Condition = 1f },
				new NetVector2(10f, 20f), new NetVector2(1f, 2f), 0f, false, 0f); // the drops
		};

		w.Trigger(w.G1, EntityEventKind.MineExploded, 10f, 20f);

		Assert.True(w.G2Events.Count == 1, $"the EntityEvent relay reaches the other guest, got {w.G2Events.Count}");
		Assert.True(g2Blocks.Count == 1 && g2Blocks[0] == (10, 11, (ushort)42),
			$"the crater rides the BlockPlaced channel, got [{string.Join(",", g2Blocks)}]");
		Assert.True(g2Drops.Count == 1 && g2Drops[0] == 500,
			$"the drops ride the item domain, got [{string.Join(",", g2Drops)}]");
	}

	[Fact]
	public void FluidInteraction_RelayedExcludingSource()
	{
		var w = EntityEventSimWorld.Create();
		var drinks = new List<FluidInteractionMsg>();
		w.G2.Services.GetRequiredService<IWorldControl>().FluidInteractionReceived += (_, msg) => drinks.Add(msg);

		w.G1.Services.GetRequiredService<IWorldControl>().SendFluidInteraction(new FluidInteractionMsg
		{
			Kind = FluidInteractionMsg.KindDrink,
			X = 2,
			Y = 3,
		});

		// The relay is the FluidInteractionHandler's own (source excluded).
		Assert.True(drinks.Count == 1, $"the other guest gets the drink, got {drinks.Count}");
		Assert.True(drinks[0].X == 2 && drinks[0].Y == 3, "the cell rides through");
	}
}
