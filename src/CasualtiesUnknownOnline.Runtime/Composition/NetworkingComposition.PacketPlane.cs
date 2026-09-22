using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Application.Kernel;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using CasualtiesUnknownOnline.Runtime.Session.Tutorial;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Runtime.Session.AdaptiveSync;
using CasualtiesUnknownOnline.Runtime.Session.Handlers;
using CasualtiesUnknownOnline.Runtime.Session.NetworkTraffic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Composition;

/// <summary>
/// The packet plane half of <see cref="NetworkingComposition"/>: the handler
/// family, the receive/send primitives and the dispatcher that routes frames to
/// them, plus the host ban list and the two traffic observers. It is a second
/// top-level file because the repo keeps one type per file; the entry point is
/// called after the session, which is what its services read.
/// </summary>
internal static class NetworkingPacketPlaneComposition
{
	/// <summary>Registers the handler family, the receive/send primitives, the dispatcher, the host ban list and the traffic observers.</summary>
	internal static void AddPacketPlane(IServiceCollection services, string? hostBanFile)
	{
		// Packet handlers: every [PacketHandler]-marked class in the Runtime
		// assembly (Session/Handlers/) is DI-registered; the dispatcher reads
		// the attribute and builds the msg → handler dictionary at startup.
		foreach (var handlerType in typeof(NetworkingPacketPlaneComposition).Assembly.GetTypes()
			.Where(t => !t.IsAbstract && typeof(IPacketHandler).IsAssignableFrom(t)))
		{
			services.AddSingleton(typeof(IPacketHandler), handlerType);
		}

		// Data plane: receive and send are independent mechanisms. The
		// receiver binds the transport and validates directions; the sender is
		// one Send primitive. The dispatcher builds the route table and routes
		// received frames to the handlers with the per-message context.
		services.AddSingleton<PacketReceiver>();
		services.AddSingleton<PacketSender>();
		// Host ban list: a persistent host-only admin surface. The file store
		// is in-memory-only when hostBanFile is null (test/default); the
		// service owns the add/remove decision and the handshake rejection.
		services.AddSingleton(p => new HostBanFileStore(
			hostBanFile, p.GetRequiredService<ILogger<HostBanFileStore>>()));
		services.AddSingleton<HostBanService>();
		services.AddSingleton<IHostBanService>(p => p.GetRequiredService<HostBanService>());
		// Whole-protocol traffic observer: PacketSender/PacketReceiver report
		// raw frame facts into it; it rolls the periodic log window (observability
		// only — no batching/rate-limit decision is made from these numbers yet).
		services.AddSingleton<NetworkTrafficMonitor>();
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<NetworkTrafficMonitor>());
		// Adaptive stream-rate query surface: reads the same peer-health
		// observation and the stream catalog, then answers the effective cadence
		// for loss-tolerant streams. No rate decision is made from traffic here
		// beyond the health/policy abstraction.
		services.AddSingleton<AdaptiveStreamRateService>();
		services.AddSingleton(p => new HandlerContext(
			p.GetRequiredService<ISessionControl>(),
			p.GetRequiredService<IEntitySyncControl>(),
			p.GetRequiredService<ICharacterDataControl>(),
			p.GetRequiredService<IWorldControl>(),
			p.GetRequiredService<IItemControl>(),
			p.GetRequiredService<IModsControl>(),
			p.GetRequiredService<ICraftControl>(),
			p.GetRequiredService<IEnemySyncControl>(),
			p.GetRequiredService<IWorldTimeControl>(),
			p.GetRequiredService<IPlayerInteractionControl>(),
			p.GetRequiredService<ITutorialClawControl>(),
			p.GetRequiredService<IKernelProtocolControl>()));
		services.AddSingleton<PacketDispatcher>();
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<PacketDispatcher>());
	}
}
