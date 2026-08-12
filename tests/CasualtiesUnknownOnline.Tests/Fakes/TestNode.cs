using System;
using System.IO;
using BepInEx.Logging;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime;
using CasualtiesUnknownOnline.Runtime.Networking;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Steam;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CasualtiesUnknownOnline.Tests.Fakes;

/// <summary>
/// One full session stack: the production composition root (CuoBootstrap —
/// every handler, the dispatcher, the domains) with the transport and the
/// Steam surface replaced by fakes. Host and guest nodes talk over the shared
/// FakeNetwork exactly like two processes over Steam — same code path, no
/// game, no Steamworks DLLs.
/// </summary>
internal sealed class TestNode : IDisposable
{
	private TestNode(ulong steamId, FakeSteamService steam, FakeTransport transport, ServiceProvider services)
	{
		SteamId = steamId;
		Steam = steam;
		Transport = transport;
		Services = services;
		Session = services.GetRequiredService<SessionService>();
	}

	internal ulong SteamId { get; }

	internal FakeSteamService Steam { get; }

	internal FakeTransport Transport { get; }

	internal ServiceProvider Services { get; }

	internal SessionService Session { get; }

	internal static TestNode Create(ulong steamId, FakeNetwork network, FakeSteamService steam)
	{
		var transport = new FakeTransport(steamId, network);
		var logDirectory = Path.Combine(Path.GetTempPath(), "cuo-tests", $"node-{steamId}");
		var services = CuoBootstrap.BuildServiceProvider(
			new ManualLogSource("test"),
			logDirectory,
			extraRegistrations: s =>
			{
				s.Replace(ServiceDescriptor.Singleton<INetworkTransport>(transport));
				s.Replace(ServiceDescriptor.Singleton<ISteamService>(steam));
				// The real SteamService/SteamTransport registrations stay in the
				// graph but must never initialize (they would touch Steamworks).
				// Replace the lifecycle entry with the fake; the transport's
				// lifecycle entry is harmless (all members are no-ops).
				s.Replace(ServiceDescriptor.Singleton(_ => (ICuoService)steam));
			});

		var node = new TestNode(steamId, steam, transport, services);
		foreach (var svc in services.GetServices<ICuoService>())
		{
			svc.Initialize(); // the plugin's Awake order — registration order
		}

		foreach (var svc in services.GetServices<ICuoService>())
		{
			svc.Start();
		}

		return node;
	}

	/// <summary>
	/// A fully handshaken host+guest pair (lobby created, guest joined, the
	/// three-leg handshake complete) — the standard setup for message-level
	/// tests. Tests needing to inject faults or observe events before the
	/// handshake build the nodes with <see cref="Create"/> directly.
	/// </summary>
	internal static (TestNode Host, TestNode Guest) CreatePair(ulong hostId, ulong guestId, ulong lobbyId)
	{
		var network = new FakeNetwork();
		var hostSteam = new FakeSteamService(hostId) { LobbyOwner = hostId, LobbyMembers = [hostId] };
		var guestSteam = new FakeSteamService(guestId) { LobbyOwner = hostId, LobbyMembers = [hostId, guestId] };
		var host = Create(hostId, network, hostSteam);
		var guest = Create(guestId, network, guestSteam);
		host.Steam.FireLobbyCreated(lobbyId);
		host.Steam.LobbyMembers = [hostId, guestId]; // the guest joined the lobby
		guest.Steam.FireLobbyEntered(lobbyId);
		return (host, guest);
	}

	/// <summary>The session's per-frame pump — driven explicitly by the tests (the fake
	/// network delivers synchronously, so only time-driven logic needs it).</summary>
	internal void Update() => ((ICuoService)Session).Update();

	public void Dispose()
	{
		foreach (var svc in Services.GetServices<ICuoService>())
		{
			svc.Stop();
		}

		Services.Dispose();
	}
}
