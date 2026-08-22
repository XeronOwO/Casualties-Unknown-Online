using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.NetworkTraffic;
using CasualtiesUnknownOnline.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Networking;

public class PacketTrafficMonitorTests
{
	private const ulong HostId = 1001;
	private const ulong GuestId = 2001;
	private const ulong LobbyId = 9001;

	[Fact]
	public void RequestPing_RecordsSendOnHostAndReceiveOnGuest()
	{
		var (host, guest) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var hostMonitor = host.Services.GetRequiredService<NetworkTrafficMonitor>();
		var guestMonitor = guest.Services.GetRequiredService<NetworkTrafficMonitor>();

		host.Session.RequestPing();

		var hostWindow = hostMonitor.CurrentWindow;
		Assert.True(hostWindow.SendByMessage.TryGetValue(NetMsg.Ping, out var hostPing));
		Assert.True(hostPing.Count >= 1);
		Assert.True(hostPing.Bytes > 0);
		Assert.True(hostWindow.ReceiveByMessage.TryGetValue(NetMsg.Pong, out var hostPong));
		Assert.True(hostPong.Count >= 1);
		Assert.True(hostPong.Bytes > 0);

		var guestWindow = guestMonitor.CurrentWindow;
		Assert.True(guestWindow.ReceiveByMessage.TryGetValue(NetMsg.Ping, out var guestPing));
		Assert.True(guestPing.Count >= 1);
		Assert.True(guestWindow.SendByMessage.TryGetValue(NetMsg.Pong, out var guestPong));
		Assert.True(guestPong.Count >= 1);
	}

	[Fact]
	public void RequestPing_RecordsPeerHealthSnapshot()
	{
		var (host, _) = TestNode.CreatePair(HostId, GuestId, LobbyId);
		var hostMonitor = host.Services.GetRequiredService<NetworkTrafficMonitor>();

		host.Session.RequestPing();

		var health = hostMonitor.HealthSnapshots.Single(s => s.SteamId == GuestId);
		Assert.True(health.LastRttMs >= 0f, $"peer health must record the RTT, was {health.LastRttMs}");
		Assert.Equal(1, health.PingsSent);
		Assert.Equal(1, health.PingsCompleted);
		Assert.Equal(0, health.PingsLost);
	}

	[Fact]
	public void PacketSender_RecordsFailedSendAsTraffic()
	{
		var network = new FakeNetwork();
		var hostTransport = new FakeTransport(1, network);
		_ = new FakeTransport(2, network);
		var clock = new FakeClock();
		var monitor = new NetworkTrafficMonitor(clock, NullLogger<NetworkTrafficMonitor>.Instance);
		var sender = new PacketSender(hostTransport, monitor);

		Assert.True(sender.TrySend(2, NetMsg.Ping));
		Assert.False(sender.TrySend(3, NetMsg.Ping));

		var window = monitor.CurrentWindow;
		Assert.Equal(2, window.SendCount);
		Assert.Equal(1, window.FailedSendCount);
		Assert.Equal(2, window.ByPeer.Count);
		Assert.Equal(1, window.ByPeer[2].SendCount);
		Assert.Equal(1, window.ByPeer[3].SendCount);
		Assert.Equal(1, window.ByPeer[3].FailedSendCount);
	}

	[Fact]
	public void PacketSender_SendToAll_RecordsOneFramePerRecipient()
	{
		var network = new FakeNetwork();
		var hostTransport = new FakeTransport(1, network);
		_ = new FakeTransport(2, network);
		_ = new FakeTransport(3, network);
		var clock = new FakeClock();
		var monitor = new NetworkTrafficMonitor(clock, NullLogger<NetworkTrafficMonitor>.Instance);
		var sender = new PacketSender(hostTransport, monitor);

		sender.SendToAll([2, 3], NetMsg.Ping, null);

		var window = monitor.CurrentWindow;
		Assert.Equal(2, window.SendCount);
		Assert.Equal(1, window.ByPeer[2].SendCount);
		Assert.Equal(1, window.ByPeer[3].SendCount);
		Assert.Equal(2, window.SendByMessage[NetMsg.Ping].Count);
	}
}
