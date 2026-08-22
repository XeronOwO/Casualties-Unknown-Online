using System;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Logging;
using CasualtiesUnknownOnline.Runtime.Networking;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Time;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.Handlers;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using CasualtiesUnknownOnline.Runtime.Session.Tutorial;
using CasualtiesUnknownOnline.Runtime.Steam;
using ManualLogSource = BepInEx.Logging.ManualLogSource;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
	/// The built container (set by the plugin after BuildServiceProvider; null
	/// before). A read-only diagnostics seam for runtime tools (HotRepl etc.) —
	/// business code must keep using constructor injection; this only answers
	/// "what is the running service graph" (same pattern as PatchBridge.Impl:
	/// the static seam is a query, never a state store).
	/// </summary>
	public static IServiceProvider? Services { get; set; }

	/// <summary>
	/// Builds the container. <paramref name="extraRegistrations"/> lets the plugin
	/// register the Game Adapter implementation (CUO.GameAdapter references the
	/// game, so the Runtime cannot reference it back).
	/// <paramref name="characterDataFile"/> is the optional host character-data
	/// disk file; null (the test composition default) keeps the store in-memory only.
	/// </summary>
	public static ServiceProvider BuildServiceProvider(
		ManualLogSource bepinExLogSource, string logDirectory, string? legacyLogPath = null,
		string? characterDataFile = null, string? modStateFile = null,
		Action<IServiceCollection>? extraRegistrations = null)
	{
		var services = new ServiceCollection();

		// Default options monitors: the production plugin replaces these with the
		// BepInEx config-backed monitors in extraRegistrations; tests replace
		// them with mutable monitors. Providers and stream services resolve the
		// monitor through DI, so a replacement here reaches every consumer.
		services.AddSingleton<IOptionsMonitor<LoggingOptions>>(
			new MutableOptionsMonitor<LoggingOptions>(new LoggingOptions()));
		services.AddSingleton<IOptionsMonitor<StateStreamOptions>>(
			new MutableOptionsMonitor<StateStreamOptions>(new StateStreamOptions()));

		// The logging providers are DI-resolved (registered as ILoggerProvider)
		// rather than captured as instances, so the extraRegistrations options
		// replacement also reaches the log sinks. The factory minimum stays Trace
		// on purpose — the providers enforce the configurable level.
		services.AddSingleton(p => new BepInExLoggerProvider(
			bepinExLogSource, p.GetRequiredService<IOptionsMonitor<LoggingOptions>>()));
		services.AddSingleton<ILoggerProvider>(p => p.GetRequiredService<BepInExLoggerProvider>());
		services.AddSingleton(p => new RollingFileLoggerProvider(
			logDirectory, legacyLogPath, p.GetRequiredService<IOptionsMonitor<LoggingOptions>>()));
		services.AddSingleton<ILoggerProvider>(p => p.GetRequiredService<RollingFileLoggerProvider>());
		services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Trace));

		// Registration order determines GetServices<ICuoService>() order:
		// SteamService before SteamTransport (transport reads steam readiness),
		// SessionService before EntitySyncService/PacketDispatcher (they read the
		// session control surface — resolved after the session is built).
		// The clock (ITimeSource) is a pure reading point — the domain services
		// derive their throttles/timeouts from it; tests replace it with a
		// virtual clock.
		services.AddSingleton<SystemTimeSource>();
		services.AddSingleton<ITimeSource>(p => p.GetRequiredService<SystemTimeSource>());
		services.AddSingleton<SteamService>();
		services.AddSingleton<ISteamService>(p => p.GetRequiredService<SteamService>());
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<SteamService>());
		services.AddSingleton<SteamTransport>();
		services.AddSingleton<INetworkTransport>(p => p.GetRequiredService<SteamTransport>());
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<SteamTransport>());

		// Session owns its state (identity/flags/presence, created internally);
		// consumers depend on the narrow ISessionControl surface, registered as
		// a factory so it resolves after the session is built (acyclic graph).
		// The session also reads the mod domain for the handshake list — the
		// IModListProvider factory resolves the registry (built later, no cycle:
		// the registry only depends on the logger).
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
			p.GetRequiredService<IWorldControl>(),
			p.GetRequiredService<IItemControl>(),
			p.GetRequiredService<IModsControl>(),
			p.GetRequiredService<ICraftControl>(),
			p.GetRequiredService<IEnemySyncControl>(),
			p.GetRequiredService<IWorldTimeControl>(),
			p.GetRequiredService<IPlayerInteractionControl>(),
			p.GetRequiredService<ITutorialClawControl>()));
		services.AddSingleton<PacketDispatcher>();
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<PacketDispatcher>());

		// Entity-sync domain: the entity table, the sync decisions and the
		// 20 Hz state exchange + join announcements. It reads the session's
		// control surface, so it runs after the session in the Update order.
		services.AddSingleton<EntitySyncService>();
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<EntitySyncService>());
		services.AddSingleton<IEntitySyncControl>(p => p.GetRequiredService<EntitySyncService>());
		// Enemy-sync domain: host-authoritative enemy snapshots (the host
		// publishes the simulated enemies, this broadcasts at 20 Hz + the
		// world-entry full snapshot; the guest receives for its render copies).
		services.AddSingleton<EnemySyncService>();
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<EnemySyncService>());
		services.AddSingleton<IEnemySyncControl>(p => p.GetRequiredService<EnemySyncService>());
		// Tutorial-claw presentation stream (host-authoritative 20 Hz claw visual;
		// no course/prop state — the Game Adapter owns the capture/apply).
		services.AddSingleton<TutorialClawService>();
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<TutorialClawService>());
		services.AddSingleton<ITutorialClawControl>(p => p.GetRequiredService<TutorialClawService>());
		// Character-data domain: the SteamID-keyed save/restore with its disk
		// store (no pump, not an ICuoService — it only reacts to reports and
		// handshakes). A null characterDataFile keeps the store in-memory-only
		// (the test composition default).
		services.AddSingleton(p => new CharacterDataFileStore(
			characterDataFile, p.GetRequiredService<ILogger<CharacterDataFileStore>>()));
		services.AddSingleton<CharacterDataStore>();
		services.AddSingleton<ICharacterDataControl>(p => p.GetRequiredService<CharacterDataStore>());
		// Remote-vitals cache: the Online UI's read-only view of the latest
		// character snapshots (no pump, not an ICuoService — only reacts to the
		// character-data stream and session end).
		services.AddSingleton<RemoteVitalsService>();
		// Remote-inventory cache: the Online UI's read-only view of the latest
		// carried/worn item snapshots (same events and lifecycle as vitals).
		services.AddSingleton<RemoteInventoryService>();
		// World domain: world-start parameters + block-damage reports (no pump,
		// not an ICuoService — it only reacts to calls and messages).
		services.AddSingleton<TrapConsumptionRegistry>(); // the one-shot trap-consumption table
		services.AddSingleton<OpenedEntityRegistry>(); // the opened lockable-entity table (the late-joiner snapshot's source)
		services.AddSingleton<BuildingEntityHealthRegistry>(); // the damaged building-entity health table (the late-joiner snapshot's source)
		services.AddSingleton<BlockDamageRegistry>(); // the partial block-damage table (the late-joiner snapshot's source)
		services.AddSingleton<TrapLayoutRegistry>(); // the generated trap-entity layout (the host's entity-distribution authority)
		services.AddSingleton<WorldTimeChannel>(); // the world-time request/broadcast channel (host authority — the Game Adapter owns the policy)
		services.AddSingleton<IWorldTimeControl>(p => p.GetRequiredService<WorldTimeChannel>());
		services.AddSingleton<EntityEventChannel>(); // the entity event/creation channels + the consumption/opened/health registries
		services.AddSingleton<TradeChannel>(); // the trader state/action channel (trade domain)
		services.AddSingleton<SpeechChannel>(); // the speech-bubble channel (the Talker domain)
		services.AddSingleton<WorldService>();
		services.AddSingleton<IWorldControl>(p => p.GetRequiredService<WorldService>());
		// Item domain: the authoritative world-item table + pickup arbitration
		// (ItemService itself reacts to calls and messages; the pending-pickup
		// hold window's expiry edge is the tiny PendingPickupPump below).
		// ItemArbitration is DI-registered so the crafting domain composes the
		// same transfer table (RemoveTransferred/AdoptEvidence/RegisterCarried).
		services.AddSingleton<ItemArbitration>();
		services.AddSingleton<ItemService>();
		services.AddSingleton<IItemControl>(p => p.GetRequiredService<ItemService>());
		// Direct player interaction (cross-player inventory take) — depends on the
		// session, character-data and item control surfaces; no pump.
		services.AddSingleton<PlayerInteractionService>();
		services.AddSingleton<IPlayerInteractionControl>(p => p.GetRequiredService<PlayerInteractionService>());
		services.AddSingleton<PendingPickupPump>();
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<PendingPickupPump>());
		// Item-traffic observer: logs the per-window item-message volume (no
		// batching/rate-limit — observe first, optimize only if the numbers hurt).
		services.AddSingleton<ItemTrafficPump>();
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<ItemTrafficPump>());
		// Crafting domain: the one-operation-one-report apply + the recipe
		// unlock (no pump, not an ICuoService — it only reacts to calls and
		// messages; ItemService's crafting seams are its world-table gateway).
		services.AddSingleton<CraftSyncService>();
		services.AddSingleton<ICraftControl>(p => p.GetRequiredService<CraftSyncService>());
		// Mod domain (Phase 4 Mod API): discovery registry (pure), the message
		// channel and the coordinator (an ICuoService — registered after the
		// session it reads; the session's IModListProvider resolves the registry
		// lazily, so this order is safe). The mod-state disk store is a
		// persistence mechanism only (no pump); a null path keeps it in-memory
		// (the test composition default).
		services.AddSingleton(p => new ModStateFileStore(
			modStateFile, p.GetRequiredService<ILogger<ModStateFileStore>>()));
		services.AddSingleton<ModRegistry>();
		services.AddSingleton<IModListProvider>(p => p.GetRequiredService<ModRegistry>());
		services.AddSingleton<ModChannel>();
		services.AddSingleton<ModService>();
		services.AddSingleton<IModsControl>(p => p.GetRequiredService<ModService>());
		services.AddSingleton<IModUiControl>(p => p.GetRequiredService<ModService>());
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<ModService>());

		extraRegistrations?.Invoke(services);

		return services.BuildServiceProvider();
	}
}
