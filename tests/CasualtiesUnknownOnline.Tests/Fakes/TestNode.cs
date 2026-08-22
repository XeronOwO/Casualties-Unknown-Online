using System;
using System.IO;
using BepInEx.Logging;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime;
using CasualtiesUnknownOnline.Runtime.Networking;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Steam;
using CasualtiesUnknownOnline.Runtime.Time;
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
	private TestNode(ulong steamId, FakeSteamService steam, FakeTransport transport, FakeClock clock, ServiceProvider services)
	{
		SteamId = steamId;
		Steam = steam;
		Transport = transport;
		Clock = clock;
		Services = services;
		Session = services.GetRequiredService<SessionService>();
	}

	internal ulong SteamId { get; }

	internal FakeSteamService Steam { get; }

	internal FakeTransport Transport { get; }

	/// <summary>The virtual clock this node runs on — the shared simulation clock (its network and its domain services read the same instance).</summary>
	internal FakeClock Clock { get; }

	internal ServiceProvider Services { get; }

	internal SessionService Session { get; }

	internal static TestNode Create(ulong steamId, FakeNetwork network, FakeSteamService steam,
		FakeClock? clock = null, bool pumpFirstFrame = false,
		string? characterDataFile = null, string? modStateFile = null,
		Action<IServiceCollection>? extraRegistrations = null)
	{
		var transport = new FakeTransport(steamId, network);
		clock ??= new FakeClock();
		var logDirectory = Path.Combine(Path.GetTempPath(), "cuo-tests", $"node-{steamId}");
		var services = CuoBootstrap.BuildServiceProvider(
			new ManualLogSource("test"),
			logDirectory,
			characterDataFile: characterDataFile,
			modStateFile: modStateFile,
			extraRegistrations: s =>
			{
				s.Replace(ServiceDescriptor.Singleton<INetworkTransport>(transport));
				s.Replace(ServiceDescriptor.Singleton<ISteamService>(steam));
				s.Replace(ServiceDescriptor.Singleton<ITimeSource>(clock));
				// The real SteamService/SteamTransport registrations stay in the
				// graph but must never initialize (they would touch Steamworks).
				// Replace BOTH lifecycle entries with the fakes — the full
				// ICuoService pump drives every Update in registration order, and
				// SteamTransport.Update polls Steamworks (FileNotFoundException in
				// a test host without the DLLs). Each Replace swaps the FIRST
				// remaining match: the SteamService entry, then the transport's.
				s.Replace(ServiceDescriptor.Singleton(_ => (ICuoService)steam));
				s.Replace(ServiceDescriptor.Singleton(_ => (ICuoService)transport));
				extraRegistrations?.Invoke(s); // the test's overrides (e.g. stub mod control surfaces) — last, so they win
			});

		var node = new TestNode(steamId, steam, transport, clock, services);
		foreach (var svc in services.GetServices<ICuoService>())
		{
			svc.Initialize(); // the plugin's Awake order — registration order
		}

		foreach (var svc in services.GetServices<ICuoService>())
		{
			svc.Start();
		}

		if (pumpFirstFrame)
		{
			// The first update frame — the mod discovery scan (BepInEx loads
			// plugins one by one, so discovery must run after every plugin's
			// Awake; a handshake arriving BEFORE it is refused as "mod check
			// pending", so standard handshake setups pump it first).
			node.Update();
		}

		return node;
	}

	/// <summary>
	/// A fully handshaken host+guest pair (lobby created, guest joined, the
	/// three-leg handshake complete) — the standard setup for message-level
	/// tests. Tests needing to inject faults or observe events before the
	/// handshake build the nodes with <see cref="Create"/> directly.
	/// </summary>
	internal static (TestNode Host, TestNode Guest) CreatePair(ulong hostId, ulong guestId, ulong lobbyId,
		Action<IServiceCollection>? extraRegistrations = null, string? characterDataFile = null,
		string? modStateFile = null)
	{
		var clock = new FakeClock();
		var network = new FakeNetwork(clock: clock);
		var hostSteam = new FakeSteamService(hostId) { LobbyOwner = hostId, LobbyMembers = [hostId] };
		var guestSteam = new FakeSteamService(guestId) { LobbyOwner = hostId, LobbyMembers = [hostId, guestId] };
		var host = Create(hostId, network, hostSteam, clock, pumpFirstFrame: true, characterDataFile: characterDataFile, modStateFile: modStateFile, extraRegistrations: extraRegistrations);
		var guest = Create(guestId, network, guestSteam, clock, pumpFirstFrame: true, characterDataFile: characterDataFile, modStateFile: modStateFile, extraRegistrations: extraRegistrations);
		host.Steam.FireLobbyCreated(lobbyId);
		host.Steam.LobbyMembers = [hostId, guestId]; // the guest joined the lobby
		guest.Steam.FireLobbyEntered(lobbyId);
		return (host, guest);
	}

	/// <summary>The session's per-frame pump — every ICuoService in registration
	/// order, exactly the production Plugin.Update loop (the domain pumps — the
	/// entity stream's 20 Hz throttle, the world gate, the session presence
	/// check — live in their own Update methods).</summary>
	internal void Update()
	{
		foreach (var svc in Services.GetServices<ICuoService>())
		{
			svc.Update();
		}
	}

	public void Dispose()
	{
		foreach (var svc in Services.GetServices<ICuoService>())
		{
			svc.Stop();
		}

		Services.Dispose();
	}
}
