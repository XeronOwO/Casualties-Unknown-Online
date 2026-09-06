using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Session.AdaptiveSync;
using CasualtiesUnknownOnline.Runtime.Session.NetworkTraffic;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session.AdaptiveSync;

public class AdaptiveStreamRateServiceTests
{
	private const ulong BadPeer = 2001;
	private const ulong HealthyPeer = 2002;

	[Fact]
	public void EmptyBroadcast_IsOptimalDefault()
	{
		var service = CreateService();

		Assert.Equal(20, service.GetEffectiveHz(AdaptiveStreamId.PlayerStateBroadcast, []));
	}

	[Fact]
	public void Broadcast_UsesMostConstrainedPeer()
	{
		var service = CreateService(withBadPeer: true);

		var result = service.GetEffectiveHz(
			AdaptiveStreamId.PlayerStateBroadcast,
			[BadPeer, HealthyPeer]);

		Assert.Equal(5, result);
	}

	[Fact]
	public void Unicast_UsesPerPeerHealth()
	{
		var service = CreateService(withBadPeer: true);

		Assert.Equal(5, service.GetEffectiveHz(AdaptiveStreamId.PlayerStateReport, BadPeer));
		Assert.Equal(20, service.GetEffectiveHz(AdaptiveStreamId.PlayerStateReport, HealthyPeer));
	}

	[Fact]
	public void UnknownStream_ReturnsConfiguredBase()
	{
		var service = CreateService();

		Assert.Equal(20, service.GetEffectiveHz((AdaptiveStreamId)999, 1));
	}

	private static AdaptiveStreamRateService CreateService(bool withBadPeer = false)
	{
		var clock = new FakeClock();
		var monitor = new NetworkTrafficMonitor(clock, NullLogger<NetworkTrafficMonitor>.Instance);
		if (withBadPeer)
		{
			monitor.RecordPingSent(BadPeer, sendTicks: 1000, nowMs: 1000);
			monitor.RecordPong(BadPeer, rttMs: 500f, echoTicks: 1000);
		}

		var options = new MutableOptionsMonitor<StateStreamOptions>(
			new StateStreamOptions { StateStreamHz = 20 });
		return new AdaptiveStreamRateService(
			monitor,
			options,
			NullLogger<AdaptiveStreamRateService>.Instance);
	}
}
