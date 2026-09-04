using System;
using System.IO;
using System.Linq;
using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.Logging;
using CasualtiesUnknownOnline.Runtime.Networking;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Time;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.ProjectionHealth;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using CasualtiesUnknownOnline.Runtime.Session.Chat;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using CasualtiesUnknownOnline.Runtime.Session.Handlers;
using CasualtiesUnknownOnline.Runtime.Session.HostRules;
using CasualtiesUnknownOnline.Runtime.Localization;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Runtime.Session.NetworkTraffic;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using CasualtiesUnknownOnline.Runtime.Session.Tutorial;
using CasualtiesUnknownOnline.Runtime.Steam;
using CasualtiesUnknownOnline.Runtime.Diagnostics;
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
		string? characterDataFile = null, string? modStateFile = null, string? hostBanFile = null,
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
		services.AddSingleton<IOptionsMonitor<RespawnOptions>>(
			new MutableOptionsMonitor<RespawnOptions>(new RespawnOptions()));
		services.AddSingleton<IOptionsMonitor<HostRulesOptions>>(
			new MutableOptionsMonitor<HostRulesOptions>(new HostRulesOptions()));
		services.AddSingleton<IOptionsMonitor<LocalizationOptions>>(
			new MutableOptionsMonitor<LocalizationOptions>(new LocalizationOptions()));

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
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<SteamService>());
		// SteamTransport takes the concrete SteamService, NOT the router: the
		// router itself composes SteamTransport, so injecting ISteamService here
		// would create a constructor cycle.
		services.AddSingleton(p => new SteamTransport(
			p.GetRequiredService<SteamService>(),
			p.GetRequiredService<ILogger<SteamTransport>>()));
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<SteamTransport>());
		// Non-Steam transport path: TCP IP-direct host/guest. The router exposes
		// the ACTIVE pair (Steam or IP-direct) through the same INetworkTransport /
		// ISteamService contracts; the plugin switches it when an IP session starts.
		services.AddSingleton<IpDirectTransport>();
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<IpDirectTransport>());
		services.AddSingleton<IpDirectSteamService>();
		services.AddSingleton<CuoNetworkRouter>();
		services.AddSingleton<INetworkTransport>(p => p.GetRequiredService<CuoNetworkRouter>());
		services.AddSingleton<ISteamService>(p => p.GetRequiredService<CuoNetworkRouter>());

		// Session owns its state (identity/flags/presence, created internally);
		// consumers depend on the narrow ISessionControl surface, registered as
		// a factory so it resolves after the session is built (acyclic graph).
		// The session also reads the mod domain for the handshake list — the
		// IModListProvider factory resolves the registry (built later, no cycle:
		// the registry only depends on the logger).
		services.AddSingleton<SessionService>();
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<SessionService>());
		services.AddSingleton<ISessionControl>(p => p.GetRequiredService<SessionService>());

		// Minimal host-rules service: a stateless composition of the host-only
		// flags and the revives/respawn flags. Registered before the handlers so
		// HandshakeHandler can inject it for the late-join gate.
		services.AddSingleton<HostRulesService>();
		services.AddSingleton<IHostRules>(p => p.GetRequiredService<HostRulesService>());
		// The host-rule write seam defaults to unavailable; the plugin replaces
		// it with the BepInEx ConfigEntry-backed editor in extraRegistrations.
		services.AddSingleton<IHostRulesEditor>(new DisabledHostRulesEditor());

		// Localization service: reads the UI language from the config-backed
		// options monitor and falls back to English for missing keys.
		services.AddSingleton<LocalizationService>();
		services.AddSingleton<ILocalizationService>(p => p.GetRequiredService<LocalizationService>());

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

		// Entity-sync domain: the entity table, the sync decisions and the
		// 20 Hz state exchange + join announcements. It reads the session's
		// control surface, so it runs after the session in the Update order.
		services.AddSingleton<PlayerKernelStatusProjection>();
		services.AddSingleton<EntitySyncService>();
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<EntitySyncService>());
		services.AddSingleton<IEntitySyncControl>(p => p.GetRequiredService<EntitySyncService>());
		// Enemy-sync domain: host-authoritative enemy snapshots (the host
		// publishes the simulated enemies, this broadcasts at 20 Hz + the
		// world-entry full snapshot; the guest receives for its render copies).
		services.AddSingleton<EnemyKernelProjection>();
		services.AddSingleton<EnemyKernelRestoreProjection>();
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
		services.AddSingleton<PlayerKernelLimbProjection>();
		services.AddSingleton<PlayerKernelRestoreProjection>();
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
		services.AddSingleton<TrapStateRegistry>(); // the trap state-machine kernel projection
		services.AddSingleton<OpenedEntityRegistry>(); // the opened lockable-entity table (the late-joiner snapshot's source)
		services.AddSingleton<BuildingEntityHealthRegistry>(); // the damaged building-entity health table (the late-joiner snapshot's source)
		services.AddSingleton<BlockDamageRegistry>(); // the partial block-damage table (the late-joiner snapshot's source)
		services.AddSingleton<TrapLayoutRegistry>(); // the generated trap-entity layout (the host's entity-distribution authority)
		services.AddSingleton<WorldTimeChannel>(); // the world-time request/broadcast channel (host authority — the Game Adapter owns the policy)
		services.AddSingleton<IWorldTimeControl>(p => p.GetRequiredService<WorldTimeChannel>());
		services.AddSingleton<EntityEventChannel>(); // the entity event/creation channels + the consumption/opened/health registries
		services.AddSingleton<TradeChannel>(); // the trader state/action channel (trade domain)
		services.AddSingleton<SpeechChannel>(); // the speech-bubble channel (the Talker domain)
		services.AddSingleton<ChatChannel>(); // the text-chat channel (co-op communication)
		services.AddSingleton<LocationPingChannel>(); // the transient middle-click location-ping channel (co-op presentation)
		services.AddSingleton<FluidKernelProjection>();
		services.AddSingleton<FluidKernelReadProjection>();
		services.AddSingleton<WorldEntityKernelProjection>();
		services.AddSingleton<WorldService>();
		services.AddSingleton<IWorldControl>(p => p.GetRequiredService<WorldService>());
		// The world-entry backfill fan-out owns the ordered snapshot group +
		// completion marker; it is injected into the handshake/scene handlers
		// so HandlerContext no longer owns a concrete world-entry flow.
		services.AddSingleton<WorldEntryFanout>();
		// Text-chat domain: the bounded recent-message buffer + send path (no
		// pump — it only reacts to the world channel's receive event and session end).
		services.AddSingleton<ChatService>();
		services.AddSingleton<IChatControl>(p => p.GetRequiredService<ChatService>());
		// Location-ping domain: the local one-marker-per-player buffer + the
		// middle-click double-click rule. No pump — it reacts to the world
		// channel's receive event, session end, and UI placement calls.
		services.AddSingleton<LocationPingService>();
		services.AddSingleton<ILocationPingControl>(p => p.GetRequiredService<LocationPingService>());
		// In-game command/chat console: local slash-command chain + the chat UI
		// surface (no wire message, no packet handler — it only rides the
		// existing ChatService send path).
		services.AddSingleton<ConsoleCommandRegistry>();
		services.AddSingleton<CommandConsoleService>();
		services.AddSingleton<ICommandControl>(p => p.GetRequiredService<CommandConsoleService>());
		services.AddSingleton<ICommandCompletionSource>(p => p.GetRequiredService<CommandConsoleService>());
		services.AddSingleton<ICommandArgumentSuggestions>(p => p.GetRequiredService<CommandConsoleService>());
		// Pure console input state machine: history, completion cycling, ESC/Enter
		// behavior. It is Unity-free and shared by the standalone overlay.
		services.AddSingleton<ConsoleInputSession>();
		// Item domain: the authoritative world-item table + pickup arbitration
		// (ItemService itself reacts to calls and messages; the pending-pickup
		// hold window's expiry edge is the tiny PendingPickupPump below).
		// ItemArbitration is DI-registered so the crafting domain composes the
		// same transfer table (RemoveTransferred/AdoptEvidence/RegisterCarried).
		services.AddSingleton<ProjectionHealthCoordinator>();
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<ProjectionHealthCoordinator>());
		services.AddSingleton<ItemArbitration>();
		services.AddSingleton<ItemKernelAuthority>();
		// Phase C four-envelope kernel protocol: executes wire commands on the
		// host, applies checkpoints/batches on the guest, and owns the host
		// journal used by join/reconnect fallback.
		services.AddSingleton<KernelProtocolService>();
		services.AddSingleton<IKernelProtocolControl>(p => p.GetRequiredService<KernelProtocolService>());
		services.AddSingleton<ItemService>();
		services.AddSingleton<IItemControl>(p => p.GetRequiredService<ItemService>());
		// Direct player-interaction visibility oracle. The base composition root
		// permits every pair; the plugin replaces it with the Game Adapter's
		// world-backed line-of-sight implementation in extraRegistrations.
		services.AddSingleton<IPlayerInteractionVisibility>(new AllowAllPlayerInteractionVisibility());
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
		// The mod status store is the Game Adapter's only ModService dependency
		// (the vanilla body/limb status projection reads it). Registering the
		// store separately breaks the ModService ↔ GameAdapter DI cycle: the
		// adapter must not depend on ModService because ModService's IMod*
		// spawner/tile/... seams are replaced by the adapter itself.
		services.AddSingleton(p => new ModStatusStore(
			p.GetRequiredService<ILogger<ModStatusStore>>()));
		// The default mod entity spawner is disabled: the Game Adapter is
		// registered by the plugin through extraRegistrations and replaces this
		// with the real Utils.Create-backed implementation. Tests may also
		// replace it with a recording fake.
		services.AddSingleton<IModEntitySpawner>(new DisabledModEntitySpawner());
		// The default mod item spawner is disabled for the same reason; the
		// Game Adapter replaces it with the real item-domain-backed
		// implementation.
		services.AddSingleton<IModItemSpawner>(new DisabledModItemSpawner());
		// The default mod tile placer is disabled for the same reason; the
		// Game Adapter replaces it with the real WorldGeneration.SetBlock-backed
		// implementation.
		services.AddSingleton<IModTilePlacer>(new DisabledModTilePlacer());
		// The default mod structure placer is disabled for the same reason; the
		// Game Adapter replaces it with the real multi-block SetBlock-backed
		// implementation.
		services.AddSingleton<IModStructurePlacer>(new DisabledModStructurePlacer());
		// The default mod liquid placer is disabled for the same reason; the
		// Game Adapter replaces it with the real FluidManager.SetLiquid/StartFill
		// backed implementation.
		services.AddSingleton<IModLiquidPlacer>(new DisabledModLiquidPlacer());
		// The default mod native-API provider is disabled for the same reason:
		// only the Game Adapter knows the game-private operations. Tests may
		// replace it with a recording fake.
		services.AddSingleton<IModNativeApiProvider>(new DisabledModNativeApiProvider());
		// Runtime building hook table: shared by ModService (per-mod adapter) and
		// the Game Adapter building content provider (the only consumer that can
		// turn hook results into Unity components).
		services.AddSingleton(p => new ModBuildingRuntimeStore(
			p.GetRequiredService<ILogger<ModBuildingRuntimeStore>>()));
		services.AddSingleton<ModService>();
		services.AddSingleton<IModsControl>(p => p.GetRequiredService<ModService>());
		services.AddSingleton<IModUiControl>(p => p.GetRequiredService<ModService>());
		services.AddSingleton<IModContentControl>(p => p.GetRequiredService<ModService>());
		services.AddSingleton<ModContentCatalog>();
		services.AddSingleton<IModContentCatalog>(p => p.GetRequiredService<ModContentCatalog>());
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<ModService>());
		// The generic content binder must run AFTER ModService's first-frame
		// discovery: mods register content during Bind, and only then can the
		// binder route definitions to per-kind providers.
		services.AddSingleton<ModContentBinder>();
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<ModContentBinder>());

		extraRegistrations?.Invoke(services);

		// Fail fast on DI cycles at the composition root. ValidateOnBuild
		// catches constructor/implementation-type cycles before startup; the
		// factory wrapper catches factory-mediated re-entrant resolution that
		// static validation cannot see.
		DiCycleGuard.WrapFactoryDescriptors(
			services,
			exception => LogCompositionRootFailure(bepinExLogSource, logDirectory, legacyLogPath, exception));
		try
		{
			return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
		}
		catch (Exception ex)
		{
			LogCompositionRootFailure(bepinExLogSource, logDirectory, legacyLogPath, ex);
			throw;
		}
	}

	private static void LogCompositionRootFailure(
		ManualLogSource bepinExLogSource,
		string logDirectory,
		string? legacyLogPath,
		Exception exception)
	{
		var message = $"CUO composition root validation failed: {exception}";
		bepinExLogSource.LogError(message);

		try
		{
			using var provider = new RollingFileLoggerProvider(
				logDirectory,
				legacyLogPath,
				new MutableOptionsMonitor<LoggingOptions>(new LoggingOptions()));
			provider.CreateLogger(nameof(CuoBootstrap))
				.LogError(exception, "CUO composition root validation failed.");
			if (provider.IsEnabled)
			{
				return;
			}
		}
		catch
		{
			// Fall through to the direct append below.
		}

		// If a standalone rolling provider cannot take the file (for example a
		// factory cycle is detected after the DI provider already owns
		// latest.log), append the startup failure directly so the CUO log still
		// gets the diagnostic.
		try
		{
			Directory.CreateDirectory(logDirectory);
			var latestLog = Path.Combine(logDirectory, "latest.log");
			File.AppendAllText(
				latestLog,
				$"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [ERR] [{nameof(CuoBootstrap)}] CUO composition root validation failed: {exception}{Environment.NewLine}");
		}
		catch
		{
			// Startup diagnostics must never mask the original build failure.
		}
	}
}
