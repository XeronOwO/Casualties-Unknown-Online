using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The player-item explosion (dynamite) star-relay scenarios: the wire
/// channel surfaces a guest's detonation report to the host, the host's
/// adapter shell applies it and relays to the other guests (source excluded),
/// and the host's own detonation broadcasts to every guest. The world
/// terrain/building/item consequences are not simulated here — they ride the
/// existing block/building/item channels and are already covered by their own
/// simulation suites; this locks the new dedicated event's topology.
/// </summary>
public class DynamiteExplosionSimulationTests
{
	[Fact]
	public void GuestDetonation_HostAppliesAndRelaysToOtherGuest_SourceExcluded()
	{
		var w = EntityEventSimWorld.Create();
		var hostApplied = new List<(ulong ItemId, NetVector2 Pos)>();
		var g2Explosions = new List<(ulong ItemId, NetVector2 Pos)>();

		w.Host.Services.GetRequiredService<IWorldControl>().DynamiteExplosionReceived += (sender, itemId, pos) =>
		{
			hostApplied.Add((itemId, pos));
			// The production GameAdapter shell: apply to the host world,
			// then relay to the other members (source excluded).
			w.Host.Services.GetRequiredService<IWorldControl>().BroadcastDynamiteExplosion(sender, itemId, pos);
		};
		w.G2.Services.GetRequiredService<IWorldControl>().DynamiteExplosionReceived += (_, itemId, pos) => g2Explosions.Add((itemId, pos));

		w.G1.Services.GetRequiredService<IWorldControl>().SendDynamiteExplosion(777ul, new NetVector2(10f, 20f));

		Assert.Single(hostApplied);
		Assert.Equal(777ul, hostApplied[0].ItemId);
		Assert.Equal(10f, hostApplied[0].Pos.X);
		Assert.Equal(20f, hostApplied[0].Pos.Y);
		Assert.Single(g2Explosions);
		Assert.Equal(777ul, g2Explosions[0].ItemId);
		Assert.Equal(10f, g2Explosions[0].Pos.X);
		Assert.Equal(20f, g2Explosions[0].Pos.Y);
	}

	[Fact]
	public void HostDetonation_BroadcastsToEveryGuest()
	{
		var w = EntityEventSimWorld.Create();
		var g1Explosions = new List<(ulong ItemId, NetVector2 Pos)>();
		var g2Explosions = new List<(ulong ItemId, NetVector2 Pos)>();
		w.G1.Services.GetRequiredService<IWorldControl>().DynamiteExplosionReceived += (_, itemId, pos) => g1Explosions.Add((itemId, pos));
		w.G2.Services.GetRequiredService<IWorldControl>().DynamiteExplosionReceived += (_, itemId, pos) => g2Explosions.Add((itemId, pos));

		w.Host.Services.GetRequiredService<IWorldControl>().SendDynamiteExplosion(888ul, new NetVector2(5f, 6f));

		Assert.Single(g1Explosions);
		Assert.Single(g2Explosions);
		Assert.Equal(888ul, g1Explosions[0].ItemId);
		Assert.Equal(5f, g1Explosions[0].Pos.X);
		Assert.Equal(888ul, g2Explosions[0].ItemId);
		Assert.Equal(6f, g2Explosions[0].Pos.Y);
	}
}
