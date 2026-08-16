using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The world-time wire flow over the real Runtime stack: guest → host request
/// and host → guest authoritative broadcast, through the actual handlers and
/// FakeNetwork. The Game Adapter policy (movement/sleep) is pure-tested in
/// WorldTimePolicyTests; this locks the plumbing it rides.
/// </summary>
public class WorldTimeFlowTests
{
	[Fact]
	public void GuestRequest_ReachesHostWithSpeed()
	{
		using var w = ItemSimWorld.Create();

		var received = new List<(ulong Sender, WorldTimeSpeed Speed)>();
		w.Host.Services.GetRequiredService<IWorldTimeControl>().RequestReceived += (sender, speed) => received.Add((sender, speed));
		w.G1.Services.GetRequiredService<IWorldTimeControl>().SendRequest(WorldTimeSpeed.SuperFast);
		w.Driver.Tick(33);

		Assert.Single(received);
		Assert.Equal(w.G1.SteamId, received[0].Sender);
		Assert.Equal(WorldTimeSpeed.SuperFast, received[0].Speed);
	}

	[Fact]
	public void HostBroadcast_ReachesGuestWithSpeed()
	{
		using var w = ItemSimWorld.Create();

		var received = new List<WorldTimeSpeed>();
		w.G1.Services.GetRequiredService<IWorldTimeControl>().TimeReceived += received.Add;
		w.Host.Services.GetRequiredService<IWorldTimeControl>().Broadcast(WorldTimeSpeed.UnconsciousFast);
		w.Driver.Tick(33);

		Assert.Single(received);
		Assert.Equal(WorldTimeSpeed.UnconsciousFast, received[0]);
	}
}
