using System;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Logging;
using CasualtiesUnknownOnline.Runtime.Networking;
using CasualtiesUnknownOnline.Runtime.Session;
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
		// SessionService before EntitySyncService (the entity domain reads the
		// session's member presence — it runs after in the Update order).
		services.AddSingleton<SteamService>();
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<SteamService>());
		services.AddSingleton<SteamTransport>();
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<SteamTransport>());
		services.AddSingleton<SessionIdentity>();
		// Shared session state + member presence: extracted from SessionService
		// so the entity/data domains depend on these instead of the session
		// itself (acyclic constructor graph).
		services.AddSingleton<MemberPresenceTable>();
		services.AddSingleton<SessionState>();
		services.AddSingleton<SessionService>();
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<SessionService>());

		// Packet handlers: every [PacketHandler]-marked class in the Runtime
		// assembly (Session/Handlers/) is DI-registered; the router reads the
		// attribute and builds the msg → handler dictionary at startup.
		services.AddSingleton<PacketRouter>();
		foreach (var handlerType in typeof(CuoBootstrap).Assembly.GetTypes()
			.Where(t => !t.IsAbstract && typeof(IPacketHandler).IsAssignableFrom(t)))
		{
			services.AddSingleton(typeof(IPacketHandler), handlerType);
		}

		// Data plane: the gateway binds the transport and dispatches. It
		// depends on SessionIdentity (not SessionService) — the dependency
		// graph is acyclic, plain constructor injection everywhere.
		services.AddSingleton<PacketGateway>();
		// Character-data domain: the SteamID-keyed save/restore (no pump, not
		// an ICuoService — it only reacts to reports and handshakes).
		services.AddSingleton<CharacterDataStore>();
		// Entity-sync domain: the entity table, the sync decisions and the
		// 20 Hz state exchange + join announcements. It reads the session's
		// member presence, so it runs after the session in the Update order.
		services.AddSingleton<EntitySyncService>();
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<EntitySyncService>());

		extraRegistrations?.Invoke(services);

		return services.BuildServiceProvider();
	}
}
