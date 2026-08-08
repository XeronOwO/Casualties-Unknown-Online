using System;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Logging;
using CasualtiesUnknownOnline.Runtime.Networking;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.Handlers;
using CasualtiesUnknownOnline.Runtime.Steam;
using ManualLogSource = BepInEx.Logging.ManualLogSource;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime;

/// <summary>
/// Assembles the CUO DI container. The plugin calls this once in Awake, then
/// forwards ICuoService lifecycle notifications from BepInEx/Unity callbacks
/// (architecture.md §5.5). Microsoft.Extensions provides the plumbing; BepInEx/Unity
/// own the lifecycle and main loop.
/// </summary>
public static class CuoBootstrap
{
	/// <summary>
	/// Builds the container. <paramref name="extraRegistrations"/> lets the plugin
	/// register the Game Adapter implementation (CUO.GameAdapter references the
	/// game, so the Runtime cannot reference it back).
	/// </summary>
	public static ServiceProvider BuildServiceProvider(
		ManualLogSource bepinExLogSource, string logDirectory, string? legacyLogPath = null,
		Action<IServiceCollection>? extraRegistrations = null)
	{
		var services = new ServiceCollection();

		services.AddLogging(builder =>
		{
			builder.SetMinimumLevel(LogLevel.Trace);
			builder.AddProvider(new BepInExLoggerProvider(bepinExLogSource));
			builder.AddProvider(new RollingFileLoggerProvider(logDirectory, legacyLogPath));
		});

		// Registration order determines GetServices<ICuoService>() order:
		// SteamService before SteamTransport (transport reads steam readiness),
		// SessionService before EntitySyncService/PacketDispatcher (they read the
		// session control surface — resolved after the session is built).
		services.AddSingleton<SteamService>();
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<SteamService>());
		services.AddSingleton<SteamTransport>();
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<SteamTransport>());

		// Session owns its state (identity/flags/presence, created internally);
		// consumers depend on the narrow ISessionControl surface, registered as
		// a factory so it resolves after the session is built (acyclic graph).
		services.AddSingleton<SessionService>();
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<SessionService>());
		services.AddSingleton<ISessionControl>(p => p.GetRequiredService<SessionService>());

		// Packet handlers: every [PacketHandler]-marked class in the Runtime
		// assembly (Session/Handlers/) is DI-registered; the dispatcher reads
		// the attribute and builds the msg → handler dictionary at startup.
		foreach (var handlerType in typeof(CuoBootstrap).Assembly.GetTypes()
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
		services.AddSingleton(p => new HandlerContext(
			p.GetRequiredService<ISessionControl>(),
			p.GetRequiredService<IEntitySyncControl>(),
			p.GetRequiredService<ICharacterDataControl>(),
			p.GetRequiredService<IWorldControl>()));
		services.AddSingleton<PacketDispatcher>();
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<PacketDispatcher>());

		// Entity-sync domain: the entity table, the sync decisions and the
		// 20 Hz state exchange + join announcements. It reads the session's
		// control surface, so it runs after the session in the Update order.
		services.AddSingleton<EntitySyncService>();
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<EntitySyncService>());
		services.AddSingleton<IEntitySyncControl>(p => p.GetRequiredService<EntitySyncService>());
		// Character-data domain: the SteamID-keyed save/restore (no pump, not
		// an ICuoService — it only reacts to reports and handshakes).
		services.AddSingleton<CharacterDataStore>();
		services.AddSingleton<ICharacterDataControl>(p => p.GetRequiredService<CharacterDataStore>());
		// World domain: world-start parameters + block-damage reports (no pump,
		// not an ICuoService — it only reacts to calls and messages).
		services.AddSingleton<WorldService>();
		services.AddSingleton<IWorldControl>(p => p.GetRequiredService<WorldService>());

		extraRegistrations?.Invoke(services);

		return services.BuildServiceProvider();
	}
}
