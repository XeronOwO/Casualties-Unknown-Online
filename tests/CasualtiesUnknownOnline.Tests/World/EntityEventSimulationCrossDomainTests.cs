using System.Collections.Generic;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// Phase-2 entity-event simulations: the cross-domain scenarios (duplicate-report consumption, crystal mimic event+spawn channels, the lost fluid region healed by the next absolute overwrite, the mine explosion message storm).
/// Split from EntityEventSimulationTests so xUnit v2 (serial inside a class) does
/// not serialize every hand-written simulation in one collection. The world and
/// the host-executor shell stay shared in EntityEventSimWorld.
/// </summary>
[Trait("Category", "Integration")]
public class EntityEventSimulationCrossDomainTests
{
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
	public void CrystalMimicTriggered_HostTrigger_IsRecordedForTheLateJoiner()
	{
		var w = EntityEventSimWorld.Create();
		var consumed = new List<IReadOnlyList<EntityEventMsg>>();
		w.G1.Services.GetRequiredService<WorldEntityKernelProjection>().TrapSnapshotProjected += list => consumed.Add(list);

		// A HOST trigger never comes back through EntityEventHandler (the host
		// is not in its own presence table) — the channel must record the
		// one-shot consumption before broadcasting, or a late joiner re-arms
		// the mimic and spawns a second crystalenemy set.
		w.Trigger(w.Host, EntityEventKind.CrystalMimicTriggered, 10f, 20f);
		w.SendCheckpoint(w.G1);

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
			if ((NetMsg)frame[0] != NetMsg.KernelEnvelope)
			{
				return;
			}

			var envelope = NetPacket.DecodePayload<ProtocolFrame>(frame);
			if (envelope.CommittedBatch is null)
			{
				return;
			}

			foreach (var @event in envelope.CommittedBatch.Batch.Events)
			{
				if (@event.Kind == WireEventKind.ItemSpawned && @event.Identity.InstanceId == 500)
				{
					g2Drops.Add(@event.Identity.InstanceId);
				}
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
}
