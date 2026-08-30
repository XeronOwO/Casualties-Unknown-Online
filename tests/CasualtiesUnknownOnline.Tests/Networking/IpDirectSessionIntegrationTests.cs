using System;
using System.IO;
using System.Linq;
using BepInEx.Logging;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime;
using CasualtiesUnknownOnline.Runtime.Networking;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Steam;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasualtiesUnknownOnline.Tests.Networking;

/// <summary>
/// End-to-end IP-direct session test: two full Runtime containers connected
/// over real loopback TCP (not the fake network), driven through the same
/// session/handshake/entity code paths as production. The Steam service is
/// deliberately not initialized; only IP-direct is active.
/// </summary>
public class IpDirectSessionIntegrationTests : IDisposable
{
	private const ulong HostId = IpDirectTransport.HostPeerId;

	private readonly ServiceProvider _hostServices;
	private readonly ServiceProvider _guestServices;
	private readonly CuoNetworkRouter _hostRouter;
	private readonly CuoNetworkRouter _guestRouter;
	private readonly SessionService _hostSession;
	private readonly SessionService _guestSession;

	public IpDirectSessionIntegrationTests()
	{
		_hostServices = CreateProvider("host");
		_guestServices = CreateProvider("guest");
		_hostRouter = _hostServices.GetRequiredService<CuoNetworkRouter>();
		_guestRouter = _guestServices.GetRequiredService<CuoNetworkRouter>();
		_hostSession = _hostServices.GetRequiredService<SessionService>();
		_guestSession = _guestServices.GetRequiredService<SessionService>();
		InitializeServices(_hostServices);
		InitializeServices(_guestServices);
	}

	[Fact]
	public void HostAndGuest_CompleteThreeLegHandshakeOverTcp()
	{
		_hostRouter.IpDirectSteam.SetDisplayName("HostPlayer");
		_guestRouter.IpDirectSteam.SetDisplayName("GuestPlayer");
		_hostRouter.UseIpDirect();
		_guestRouter.UseIpDirect();
		Assert.True(_hostRouter.IpDirectSteam.StartHost(0, out var error), $"host start failed: {error}");
		var port = _hostRouter.IpDirectTransport.BoundPort;
		Assert.True(_guestRouter.IpDirectSteam.Connect("127.0.0.1", port, out error), $"guest connect failed: {error}");

		var handshaken = false;
		var deadline = Environment.TickCount + 8000;
		while (Environment.TickCount < deadline && !handshaken)
		{
			Update(_hostServices);
			Update(_guestServices);
			handshaken = _hostSession.Members.Any(m => m.SteamId != HostId && m.Handshaken)
				&& _guestSession.Members.Any(m => m.SteamId == HostId && m.Handshaken);
		}

		Assert.True(handshaken, "IP-direct handshake must complete end-to-end over TCP");
		Assert.Equal(SessionRole.Host, _hostSession.Role);
		Assert.Equal(SessionRole.Guest, _guestSession.Role);
	}

	[Fact]
	public void HostAndGuest_CarryCustomDisplayNamesThroughHandshake()
	{
		_hostRouter.IpDirectSteam.SetDisplayName("HostPlayer");
		_guestRouter.IpDirectSteam.SetDisplayName("GuestPlayer");
		_hostRouter.UseIpDirect();
		_guestRouter.UseIpDirect();
		Assert.True(_hostRouter.IpDirectSteam.StartHost(0, out var error), $"host start failed: {error}");
		Assert.True(_guestRouter.IpDirectSteam.Connect("127.0.0.1", _hostRouter.IpDirectTransport.BoundPort, out error), $"guest connect failed: {error}");

		var seen = false;
		var deadline = Environment.TickCount + 8000;
		while (Environment.TickCount < deadline && !seen)
		{
			Update(_hostServices);
			Update(_guestServices);
			seen = _hostSession.Members.Any(m => m.DisplayName == "GuestPlayer")
				&& _guestSession.Members.Any(m => m.DisplayName == "HostPlayer");
		}

		Assert.True(seen, "custom display names must cross the IP-direct handshake");
	}

	public void Dispose()
	{
		_hostRouter.IpDirectSteam.Disconnect();
		_guestRouter.IpDirectSteam.Disconnect();
		_hostServices.Dispose();
		_guestServices.Dispose();
	}

	private static ServiceProvider CreateProvider(string name)
	{
		var logDirectory = Path.Combine(Path.GetTempPath(), "cuo-ipdirect-tests", name);
		return CuoBootstrap.BuildServiceProvider(
			new ManualLogSource("test"),
			logDirectory);
	}

	private static void InitializeServices(ServiceProvider services)
	{
		// Do NOT initialize SteamService: doing so would touch Steamworks. Every
		// other service's Initialize is safe and necessary (mod discovery etc.).
		foreach (var service in services.GetServices<ICuoService>())
		{
			if (service is SteamService)
			{
				continue;
			}

			service.Initialize();
			service.Start();
		}
	}

	private static void Update(ServiceProvider services)
	{
		foreach (var service in services.GetServices<ICuoService>())
		{
			service.Update();
		}
	}
}
