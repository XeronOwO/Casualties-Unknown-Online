using System;
using System.IO;
using BepInEx.Logging;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime;
using CasualtiesUnknownOnline.Runtime.Networking;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using CasualtiesUnknownOnline.Runtime.Steam;
using CasualtiesUnknownOnline.Runtime.Time;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CasualtiesUnknownOnline.Tests.Fakes;

/// <summary>
/// One full session stack: the production composition root (CuoBootstrap —
/// every handler, the dispatcher, the domains) with the transport and the
/// Steam surface replaced by fakes and the rolling file sink removed (see
/// <see cref="TestLogging"/>). Host and guest nodes talk over the shared
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
		string? modStateFile = null, string? hostBanFile = null,
		string? savesRoot = null,
		Action<IServiceCollection>? extraRegistrations = null)
	{
		var transport = new FakeTransport(steamId, network);
		clock ??= new FakeClock();
		// Unique per node so that a test which deliberately re-enables the file
		// sink (or a composition-root failure log) never shares a latest.log with
		// a parallel node. The default test composition does not create the file.
		var logDirectory = Path.Combine(Path.GetTempPath(), "cuo-tests", $"node-{steamId}-{Guid.NewGuid():N}");
		var services = CuoBootstrap.BuildServiceProvider(
			new ManualLogSource("test"),
			logDirectory,
			modStateFile: modStateFile,
			hostBanFile: hostBanFile,
			// The world archive is disabled by default (a test node plays without one);
			// a suite that drives a real cut/continue hands in a root, and two nodes over
			// ONE root are two processes over one world.
			savesRoot: savesRoot,
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
				// The game's own content facts behind the limb-use chains: a test
				// host has no game scene, so the suite's stub of the vanilla item
				// and liquid registries answers instead (FakeLimbUseSemantics).
				// A test that needs a different verdict replaces it here.
				s.Replace(ServiceDescriptor.Singleton<ILimbUseSemantics>(FakeLimbUseSemantics.Instance));
				// The same stand-in for the consume (drink) chain's content facts,
				// whose production answer is Item.GlobalItems (FakeConsumeSemantics).
				s.Replace(ServiceDescriptor.Singleton<IConsumeSemantics>(FakeConsumeSemantics.Instance));
				// The same stand-in for the wearable (wear) chain's content facts,
				// whose production answer is Item.GlobalItems plus the live limb
				// layout (FakeWearSemantics).
				s.Replace(ServiceDescriptor.Singleton<IWearSemantics>(FakeWearSemantics.Instance));
				// The same stand-in for the SOLID-FOOD chain's content facts, whose
				// production answer is the item's own use-action shape read from its
				// compiled body (FakeSolidFoodSemantics).
				s.Replace(ServiceDescriptor.Singleton<ISolidFoodSemantics>(FakeSolidFoodSemantics.Instance));
				// Transport IDENTITY is its own seam and the save layer reads it (the world
				// folder's display name, the key space a character is written in). The
				// production identity is the network router, whose display name asks
				// Steamworks for the persona — not loadable in a test host — so every node
				// advertises its own id and a name of its own: a cut taken by two nodes that
				// shared one identity would write one player's character under both.
				s.Replace(ServiceDescriptor.Singleton<ITransportIdentity>(new FakeTransportIdentity
				{
					LocalPeerId = steamId,
					LocalDisplayName = $"Player-{steamId}",
				}));
				TestLogging.RemoveFileSink(s); // before the test's overrides, so a test may still re-add a sink
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
		Action<IServiceCollection>? extraRegistrations = null,
		string? modStateFile = null, string? hostBanFile = null,
		NetMsg? swallowHostToGuest = null)
	{
		var clock = new FakeClock();
		var network = new FakeNetwork(clock: clock);
		var hostSteam = new FakeSteamService(hostId) { LobbyOwner = hostId, LobbyMembers = [hostId] };
		var guestSteam = new FakeSteamService(guestId) { LobbyOwner = hostId, LobbyMembers = [hostId, guestId] };
		var host = Create(hostId, network, hostSteam, clock, pumpFirstFrame: true, modStateFile: modStateFile, hostBanFile: hostBanFile, extraRegistrations: extraRegistrations);
		var guest = Create(guestId, network, guestSteam, clock, pumpFirstFrame: true, modStateFile: modStateFile, hostBanFile: hostBanFile, extraRegistrations: extraRegistrations);
		if (swallowHostToGuest is { } dropped)
		{
			// Armed BEFORE the guest joins the lobby: the handshake is driven by that edge,
			// so a fault set afterwards would arrive after the frame it is meant to swallow
			// (the one-message swallow the lazy Steam P2P session performs).
			network.SetFaults(hostId, guestId, new LinkFaults { DropMessageId = dropped });
		}

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
