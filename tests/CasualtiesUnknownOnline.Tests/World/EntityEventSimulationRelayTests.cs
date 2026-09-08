using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// Phase-2 entity-event simulations: the relay topology (source-excluded relay to the other guests, host broadcast, the fluid channel and the spawn relay).
/// Split from EntityEventSimulationTests so xUnit v2 (serial inside a class) does
/// not serialize every hand-written simulation in one collection. The world and
/// the host-executor shell stay shared in EntityEventSimWorld.
/// </summary>
public class EntityEventSimulationRelayTests
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

	[Fact]
	public void FluidPresentation_HostSendsToGuest_ReplaySurfaceFires()
	{
		var w = EntityEventSimWorld.Create();
		var presentations = new List<FluidPresentationMsg>();
		w.G1.Services.GetRequiredService<IWorldControl>().FluidPresentationReceived += msg => presentations.Add(msg);

		w.Host.Services.GetRequiredService<IWorldControl>().SendFluidPresentation(w.G1.SteamId, new FluidPresentationMsg
		{
			Kind = FluidPresentationMsg.KindWaterPush,
			X = 4,
			Y = 5,
			DirX = 1f,
			DirY = 0f,
		});

		Assert.True(presentations.Count == 1, $"the guest gets the fluid presentation, got {presentations.Count}");
		Assert.True(presentations[0].Kind == FluidPresentationMsg.KindWaterPush
			&& presentations[0].X == 4 && presentations[0].Y == 5
			&& presentations[0].DirX == 1f && presentations[0].DirY == 0f,
			"the push event rides through unchanged");
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
}
