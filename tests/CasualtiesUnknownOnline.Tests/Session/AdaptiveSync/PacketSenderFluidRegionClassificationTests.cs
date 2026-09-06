using CasualtiesUnknownOnline.Protocol.Wire;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.NetworkTraffic;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Session.AdaptiveSync;

public class PacketSenderFluidRegionClassificationTests
{
	private const ulong HostId = 1001;
	private const ulong PeerId = 2001;

	[Fact]
	public void FluidRegionMessages_ClassifyDiffAndFullSeparately()
	{
		var clock = new FakeClock();
		var network = new FakeNetwork(clock: clock);
		var transport = new FakeTransport(HostId, network);
		var monitor = new NetworkTrafficMonitor(clock, NullLogger<NetworkTrafficMonitor>.Instance);
		var sender = new PacketSender(transport, monitor);

		sender.Send(PeerId, NetMsg.FluidRegion, new FluidRegionMsg
		{
			Seq = 1,
			OriginX = 0,
			OriginY = 0,
			Width = 16,
			Height = 4,
			Cells = [1, 4],
		}, reliable: false);

		sender.Send(PeerId, NetMsg.FluidRegion, new FluidRegionMsg
		{
			Seq = 2,
			OriginX = -64,
			OriginY = -64,
			Width = 128,
			Height = 112,
			Cells = [0, 1],
			FullViewport = true,
		}, reliable: false);

		// A diff whose changed-cell bounding box happens to cover the entire
		// viewport must still be counted as diff; the classification comes from
		// the sender-side hint, never from dimensions.
		sender.Send(PeerId, NetMsg.FluidRegion, new FluidRegionMsg
		{
			Seq = 3,
			OriginX = -64,
			OriginY = -64,
			Width = 128,
			Height = 112,
			Cells = [1, 4],
			FullViewport = false,
		}, reliable: false);

		var window = monitor.CurrentWindow;
		Assert.True(window.SendByPeerPayloadType.TryGetValue((PeerId, WirePayloadType.FluidRegionDiff), out var diff));
		Assert.Equal(2, diff.Count);
		Assert.True(window.SendByPeerPayloadType.TryGetValue((PeerId, WirePayloadType.FluidRegionFull), out var full));
		Assert.Equal(1, full.Count);
	}
}
