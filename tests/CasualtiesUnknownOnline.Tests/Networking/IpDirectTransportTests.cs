using System;
using System.Linq;
using System.Threading;
using CasualtiesUnknownOnline.Runtime.Networking;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Networking;

/// <summary>
/// L0 socket-level tests for the TCP IP-direct transport: host/guest connect,
/// logical peer-id mapping, bidirectional frame delivery and disconnect cleanup.
/// They run against real loopback TCP on the test machine, so they validate the
/// same transport-level hello/framing code the game uses on a LAN.
/// </summary>
public class IpDirectTransportTests : IDisposable
{
	private readonly IpDirectTransport _host = new(NullLogger<IpDirectTransport>.Instance);
	private readonly IpDirectTransport _guest = new(NullLogger<IpDirectTransport>.Instance);

	[Fact]
	public void HostAndGuest_ExchangeFrames_WithLogicalPeerIds()
	{
		Assert.True(_host.StartHost(0, out var error), $"host start failed: {error}");
		Assert.True(_host.BoundPort > 0, "host must expose its bound port after StartHost(0)");
		Assert.True(_guest.Connect("127.0.0.1", _host.BoundPort, out error), $"guest connect failed: {error}");

		var guestId = WaitFor(() => _host.ActiveRemotePeers.SingleOrDefault(), () => _host.ActiveRemotePeers.Count > 0);
		Assert.NotEqual(0ul, guestId);
		Assert.NotEqual(IpDirectTransport.HostPeerId, guestId);
		Assert.True(_guest.ActiveRemotePeers.Contains(IpDirectTransport.HostPeerId), "guest must see host id 1");
		Assert.NotEqual(0ul, _guest.LocalPeerId);
		Assert.NotEqual(IpDirectTransport.HostPeerId, _guest.LocalPeerId);

		var hostPayload = new byte[] { 0xAA, 0xBB, 0xCC };
		byte[]? guestReceived = null;
		_guest.MessageReceived += (from, data) =>
		{
			if (from == IpDirectTransport.HostPeerId)
			{
				guestReceived = data;
			}
		};

		Assert.True(_host.SendTo(guestId, hostPayload, reliable: true));
		WaitFor(() => guestReceived != null, () => guestReceived is not null, pump: () => _guest.Poll(), timeoutMs: 5000);
		Assert.Equal(hostPayload, guestReceived);

		var guestPayload = new byte[] { 0x01, 0x02, 0x03 };
		byte[]? hostReceived = null;
		_host.MessageReceived += (from, data) =>
		{
			if (from == guestId)
			{
				hostReceived = data;
			}
		};

		Assert.True(_guest.SendTo(IpDirectTransport.HostPeerId, guestPayload, reliable: true));
		WaitFor(() => hostReceived != null, () => hostReceived is not null, pump: () => _host.Poll(), timeoutMs: 5000);
		Assert.Equal(guestPayload, hostReceived);
	}

	[Fact]
	public void GuestDisconnect_RemovesPeerFromHostActiveList()
	{
		Assert.True(_host.StartHost(0, out var error), $"host start failed: {error}");
		Assert.True(_guest.Connect("127.0.0.1", _host.BoundPort, out error), $"guest connect failed: {error}");
		var guestId = WaitFor(() => _host.ActiveRemotePeers.SingleOrDefault(), () => _host.ActiveRemotePeers.Count > 0);
		Assert.NotEqual(0ul, guestId);

		_guest.Disconnect();
		WaitFor(() => !_host.ActiveRemotePeers.Contains(guestId), () => !_host.ActiveRemotePeers.Contains(guestId), timeoutMs: 5000);

		Assert.Empty(_host.ActiveRemotePeers);
	}

	public void Dispose()
	{
		_guest.Disconnect();
		_host.Disconnect();
	}

	private static T WaitFor<T>(Func<T> getValue, Func<bool> condition, Action? pump = null, int timeoutMs = 5000)
	{
		var deadline = Environment.TickCount + timeoutMs;
		while (Environment.TickCount < deadline)
		{
			pump?.Invoke();
			if (condition())
			{
				return getValue();
			}

			Thread.Sleep(10);
		}

		Assert.True(condition(), "condition timed out");
		return getValue();
	}
}
