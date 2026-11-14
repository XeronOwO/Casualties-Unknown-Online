using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Session;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session;

/// <summary>
/// The heartbeat owns only WHEN a ping goes out: the first tick arms the schedule one interval in
/// (an entry-window frame count must not see a ping on its first tick), then every interval fires
/// exactly one ping, independent of the world.
/// </summary>
public sealed class SessionHeartbeatTests
{
	[Fact]
	public void TheFirstPingWaitsOneInterval()
	{
		var now = 1_000L;
		var pings = new List<long>();
		var heartbeat = new SessionHeartbeat(() => now, () => pings.Add(now));

		heartbeat.Tick();
		Assert.Empty(pings);

		now += 4_999;
		heartbeat.Tick();
		Assert.Empty(pings);

		now += 1;
		heartbeat.Tick();
		Assert.Single(pings);
	}

	[Fact]
	public void EveryIntervalPingsOnceAgain()
	{
		var now = 0L;
		var pings = new List<long>();
		var heartbeat = new SessionHeartbeat(() => now, () => pings.Add(now));

		heartbeat.Tick();
		now += 5_000;
		heartbeat.Tick();
		Assert.Single(pings);

		// A tick inside the next interval does not ping; the following interval tick does.
		now += 2_000;
		heartbeat.Tick();
		Assert.Single(pings);

		now += 3_000;
		heartbeat.Tick();
		Assert.Equal(2, pings.Count);
	}
}
