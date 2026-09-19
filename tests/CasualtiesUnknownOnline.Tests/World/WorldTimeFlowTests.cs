using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.World;

/// <summary>
/// The world-time wire flow over the real Runtime stack: guest → host report of
/// a locally applied speed and host → guest authoritative broadcast (the change,
/// the answer to a request, the world-entry fan-out and the 5 s resend), through
/// the actual handlers and FakeNetwork. The Game Adapter's arbitration and the
/// local-initiation reconciliation are pure-tested in WorldTimePolicyTests /
/// WorldTimeLocalInitiationTests; this locks the plumbing they ride.
/// </summary>
[Trait("Category", "Integration")]
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

	[Fact]
	public void RequestAndAnswer_RoundTripThroughTheRealStack()
	{
		using var w = ItemSimWorld.Create();

		// The host side of the answer is the Game Adapter's `Answer()` call; this
		// pins the plumbing it rides: report up, authoritative speed back down.
		var requested = new List<WorldTimeSpeed>();
		w.Host.Services.GetRequiredService<IWorldTimeControl>().RequestReceived += (_, speed) => requested.Add(speed);
		var answered = new List<WorldTimeSpeed>();
		w.G1.Services.GetRequiredService<IWorldTimeControl>().TimeReceived += answered.Add;

		w.G1.Services.GetRequiredService<IWorldTimeControl>().SendRequest(WorldTimeSpeed.Fast);
		w.Driver.Tick(33);
		Assert.Equal([WorldTimeSpeed.Fast], requested);

		w.Host.Services.GetRequiredService<IWorldTimeControl>().Broadcast(requested[0]);
		w.Driver.Tick(33);

		Assert.Equal([WorldTimeSpeed.Fast], answered);
	}
}
