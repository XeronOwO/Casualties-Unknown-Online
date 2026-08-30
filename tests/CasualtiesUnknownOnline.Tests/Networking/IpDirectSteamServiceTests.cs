using System;
using System.Linq;
using System.Threading;
using CasualtiesUnknownOnline.Runtime.Networking;
using CasualtiesUnknownOnline.Runtime.Steam;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Networking;

/// <summary>
/// L0 tests for the IP-direct lobby identity adapter: host/guest role events,
/// synthetic lobby membership and custom display-name lookup.
/// </summary>
public class IpDirectSteamServiceTests : IDisposable
{
	private readonly IpDirectTransport _hostTransport;
	private readonly IpDirectTransport _guestTransport;
	private readonly IpDirectSteamService _host;
	private readonly IpDirectSteamService _guest;

	public IpDirectSteamServiceTests()
	{
		_hostTransport = new IpDirectTransport(NullLogger<IpDirectTransport>.Instance);
		_guestTransport = new IpDirectTransport(NullLogger<IpDirectTransport>.Instance);
		_host = new IpDirectSteamService(_hostTransport, NullLogger<IpDirectSteamService>.Instance);
		_guest = new IpDirectSteamService(_guestTransport, NullLogger<IpDirectSteamService>.Instance);
	}

	[Fact]
	public void HostAndGuest_ReportIdentityAndCustomNames()
	{
		var hostCreated = 0;
		var guestEntered = 0;
		_host.LobbyCreated += _ => hostCreated++;
		_guest.LobbyEntered += _ => guestEntered++;
		_host.SetDisplayName("Alice");
		_guest.SetDisplayName("Bob");

		Assert.True(_host.StartHost(0, out var error), $"host start failed: {error}");
		Assert.True(_guest.Connect("127.0.0.1", _hostTransport.BoundPort, out error), $"guest connect failed: {error}");
		Assert.Equal(1, hostCreated);
		Assert.Equal(1, guestEntered);
		Assert.Equal(CasualtiesUnknownOnline.Runtime.Networking.IpDirectTransport.HostPeerId, _host.LocalSteamId);
		Assert.NotEqual(0ul, _guest.LocalSteamId);
		Assert.Equal("Alice", _host.GetPersonaName(_host.LocalSteamId));
		Assert.Equal("Bob", _guest.GetPersonaName(_guest.LocalSteamId));

		var hostSeesGuest = WaitFor(() => _hostTransport.ActiveRemotePeers.SingleOrDefault(), () => _hostTransport.ActiveRemotePeers.Count > 0);
		Assert.NotEqual(0ul, hostSeesGuest);
		Assert.Contains(hostSeesGuest, _host.GetLobbyMembers());
		Assert.Contains(CasualtiesUnknownOnline.Runtime.Networking.IpDirectTransport.HostPeerId, _guest.GetLobbyMembers());
		Assert.Equal(CasualtiesUnknownOnline.Runtime.Networking.IpDirectTransport.HostPeerId, _host.GetLobbyOwner());
		Assert.Equal(CasualtiesUnknownOnline.Runtime.Networking.IpDirectTransport.HostPeerId, _guest.GetLobbyOwner());
	}

	[Fact]
	public void Disconnect_RaisesLobbyLeftAndClearsMembers()
	{
		_host.SetDisplayName("Alice");
		_guest.SetDisplayName("Bob");
		Assert.True(_host.StartHost(0, out var error), $"host start failed: {error}");
		Assert.True(_guest.Connect("127.0.0.1", _hostTransport.BoundPort, out error), $"guest connect failed: {error}");
		var left = 0;
		_guest.LobbyLeft += _ => left++;
		_guest.Disconnect();
		Assert.Equal(1, left);
		Assert.False(_guest.IsActive);
		Assert.Empty(_guest.GetLobbyMembers());
	}

	[Fact]
	public void StartHost_WithEmptyDisplayName_IsRefused()
	{
		Assert.False(_host.StartHost(0, out var error));
		Assert.Contains("Display name", error);
		Assert.False(_host.IsActive);
		Assert.Empty(_hostTransport.ActiveRemotePeers);
	}

	[Fact]
	public void Connect_WithEmptyDisplayName_IsRefusedBeforeConnecting()
	{
		_host.SetDisplayName("Alice");
		Assert.True(_host.StartHost(0, out var hostError), $"host start failed: {hostError}");
		Assert.False(_guest.Connect("127.0.0.1", _hostTransport.BoundPort, out var error));
		Assert.Contains("Display name", error);
		Assert.False(_guest.IsActive);
		Assert.Empty(_guest.GetLobbyMembers());
	}

	public void Dispose()
	{
		_guest.Disconnect();
		_host.Disconnect();
	}

	private static T WaitFor<T>(Func<T> getValue, Func<bool> condition, int timeoutMs = 5000)
	{
		var deadline = Environment.TickCount + timeoutMs;
		while (Environment.TickCount < deadline)
		{
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
