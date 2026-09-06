using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Protocol;
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

	[Fact]
	public void MedicalStreams_UseProfileBaseHzInOptimal()
	{
		var service = CreateService();

		Assert.Equal(60, service.GetEffectiveHz(AdaptiveStreamId.MedicalInjectionReport, HealthyPeer));
		Assert.Equal(60, service.GetEffectiveHz(AdaptiveStreamId.ShrapnelPositionReport, HealthyPeer));
	}

	[Fact]
	public void HighPerStreamBandwidth_LowersCadence()
	{
		var (service, monitor, clock) = CreateServiceWithMonitor();
		for (var i = 0; i < 400; i++)
		{
			monitor.RecordSend(BadPeer, NetMsg.KernelEnvelope, 1000, true, WirePayloadType.PlayerStateStream);
		}

		clock.Advance(1000);
		var badPeerRate = service.GetEffectiveHz(AdaptiveStreamId.PlayerStateBroadcast, BadPeer);
		var healthyPeerRate = service.GetEffectiveHz(AdaptiveStreamId.PlayerStateBroadcast, HealthyPeer);

		Assert.True(badPeerRate < 20, "high measured per-stream bandwidth must lower the loss-tolerant cadence.");
		Assert.Equal(20, healthyPeerRate);
	}

	[Fact]
	public void FailedSendRate_LowersCadence()
	{
		var (service, monitor, clock) = CreateServiceWithMonitor();
		for (var i = 0; i < 100; i++)
		{
			monitor.RecordSend(BadPeer, NetMsg.KernelEnvelope, 1000, success: false, WirePayloadType.PlayerStateStream);
		}

		clock.Advance(1000);
		var badPeerRate = service.GetEffectiveHz(AdaptiveStreamId.PlayerStateBroadcast, BadPeer);

		Assert.True(badPeerRate < 20, "failed-send evidence must be treated as pressure and lower the cadence.");
	}

	[Fact]
	public void Broadcast_UsesMostConstrainedPeerForBandwidth()
	{
		var (service, monitor, clock) = CreateServiceWithMonitor();
		for (var i = 0; i < 400; i++)
		{
			monitor.RecordSend(BadPeer, NetMsg.KernelEnvelope, 1000, true, WirePayloadType.PlayerStateStream);
		}

		clock.Advance(1000);
		var result = service.GetEffectiveHz(
			AdaptiveStreamId.PlayerStateBroadcast,
			[BadPeer, HealthyPeer]);

		Assert.True(result < 20, "broadcast must use the most bandwidth-constrained peer.");
	}

	[Fact]
	public void ImmatureWindow_DoesNotCreateFalseBandwidthPressure()
	{
		var (service, monitor, _) = CreateServiceWithMonitor();
		for (var i = 0; i < 20; i++)
		{
			monitor.RecordSend(BadPeer, NetMsg.KernelEnvelope, 100_000, true, WirePayloadType.PlayerStateStream);
		}

		var rate = service.GetEffectiveHz(AdaptiveStreamId.PlayerStateBroadcast, BadPeer);

		Assert.Equal(20, rate);
	}

	[Fact]
	public void ResetMidWindow_DoesNotReuseOldTrafficPressure()
	{
		var clock = new FakeClock();
		var monitor = new NetworkTrafficMonitor(clock, NullLogger<NetworkTrafficMonitor>.Instance);
		for (var i = 0; i < 400; i++)
		{
			monitor.RecordSend(BadPeer, NetMsg.KernelEnvelope, 1000, true, WirePayloadType.PlayerStateStream);
		}

		clock.Advance(5000);
		var options = new MutableOptionsMonitor<StateStreamOptions>(
			new StateStreamOptions { StateStreamHz = 20 });
		var service = new AdaptiveStreamRateService(
			monitor,
			options,
			NullLogger<AdaptiveStreamRateService>.Instance);
		Assert.True(service.GetEffectiveHz(AdaptiveStreamId.PlayerStateBroadcast, BadPeer) < 20);

		monitor.Reset();

		Assert.Equal(20, service.GetEffectiveHz(AdaptiveStreamId.PlayerStateBroadcast, BadPeer));
	}

	[Fact]
	public void EmptyCurrentWindow_FallsBackToLastCompletedWindow()
	{
		var clock = new FakeClock();
		var monitor = new NetworkTrafficMonitor(clock, NullLogger<NetworkTrafficMonitor>.Instance);
		for (var i = 0; i < 400; i++)
		{
			monitor.RecordSend(BadPeer, NetMsg.KernelEnvelope, 1000, true, WirePayloadType.PlayerStateStream);
		}

		clock.Advance(10_000);
		((ICuoService)monitor).Update();
		Assert.Equal(0, monitor.CurrentWindow.SendCount);

		var options = new MutableOptionsMonitor<StateStreamOptions>(
			new StateStreamOptions { StateStreamHz = 20 });
		var service = new AdaptiveStreamRateService(
			monitor,
			options,
			NullLogger<AdaptiveStreamRateService>.Instance);

		var rate = service.GetEffectiveHz(AdaptiveStreamId.PlayerStateBroadcast, BadPeer);

		Assert.True(rate < 20, "the rate service must use the retained last window after a roll.");
	}

	private static AdaptiveStreamRateService CreateService(bool withBadPeer = false) =>
		CreateServiceWithMonitor(withBadPeer).Service;

	private static (AdaptiveStreamRateService Service, NetworkTrafficMonitor Monitor, FakeClock Clock) CreateServiceWithMonitor(bool withBadPeer = false)
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
		var service = new AdaptiveStreamRateService(
			monitor,
			options,
			NullLogger<AdaptiveStreamRateService>.Instance);
		return (service, monitor, clock);
	}
}
