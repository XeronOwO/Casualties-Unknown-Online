using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.ProjectionHealth;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Runtime.Time;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Composition;

/// <summary>
/// The world domain: the per-layer tables and the channels that carry their facts,
/// the kernel projections a restore replays into, the <c>WorldService</c> facade
/// that owns the world-fact lifecycle, the world-entry fan-out, and the two time
/// edges the guest converges through (the pending-report fallback pump and the
/// bounded readiness window). It registers after the entity/character data plane,
/// whose services the facade reads, and before the presentation and item blocks
/// that consume its channels.
/// </summary>
internal static class WorldComposition
{
	/// <summary>Registers the world tables, channels, projections, facade and fallback time edges.</summary>
	internal static void AddWorld(IServiceCollection services)
	{
		// World domain: world-start parameters + block-damage reports (no pump,
		// not an ICuoService — it only reacts to calls and messages).
		services.AddSingleton<TrapConsumptionRegistry>(); // the one-shot trap-consumption table
		services.AddSingleton<TrapStateRegistry>(); // the trap state-machine kernel projection
		services.AddSingleton<OpenedEntityRegistry>(); // the opened lockable-entity table (the late-joiner snapshot's source)
		services.AddSingleton<BuildingEntityHealthRegistry>(); // the damaged building-entity health table (the late-joiner snapshot's source)
		services.AddSingleton<TrapLayoutRegistry>(); // the generated trap-entity layout (the host's entity-distribution authority)
		services.AddSingleton<RuntimeEntityRegistry>(); // the host's accepted runtime-created entity table (the E3 backfill's source)
		services.AddSingleton<WorldTimeChannel>(); // the world-time request/broadcast channel (host authority — the Game Adapter owns the policy)
		services.AddSingleton<IWorldTimeControl>(p => p.GetRequiredService<WorldTimeChannel>());
		services.AddSingleton<EntityEventChannel>(); // the entity-event channel + the consumption/opened/health/layout registries
		services.AddSingleton<RuntimeEntityChannel>(); // the runtime entity-creation channel + its E3 recovery tables
		services.AddSingleton<TradeChannel>(); // the trader state/action channel (trade domain)
		services.AddSingleton<SpeechChannel>(); // the speech-bubble channel (the Talker domain)
		services.AddSingleton<ChatChannel>(); // the text-chat channel (co-op communication)
		services.AddSingleton<LocationPingChannel>(); // the transient middle-click location-ping channel (co-op presentation)
		services.AddSingleton<FluidKernelProjection>();
		services.AddSingleton<FluidKernelReadProjection>();
		services.AddSingleton<WorldEntityKernelProjection>();
		// The world-entity half of a restore, as the save layer sees it: a guest's
		// checkpoint lands immediately (its live world IS the restored layer), while
		// the host/solo side holds the restored per-entity facts until the
		// world-entry seam of the layer the restore regenerates.
		services.AddSingleton<IRestoredWorldEntitySource>(p => p.GetRequiredService<WorldEntityKernelProjection>());
		// The facade takes the native world-fact reader through a FACTORY so its
		// optionality survives DI: the adapter registers the reader through
		// extraRegistrations, and a Runtime-only composition (the test host) must
		// resolve to null rather than fail to construct the facade. The partial
		// block damage has no Runtime table at all — the game's own list is the
		// only one, and the snapshot reads it through that same port.
		services.AddSingleton(p => new WorldService(
			p.GetRequiredService<ISessionControl>(),
			p.GetRequiredService<PacketSender>(),
			p.GetRequiredService<ITimeSource>(),
			p.GetRequiredService<ILogger<WorldService>>(),
			p.GetRequiredService<EntityEventChannel>(),
			p.GetRequiredService<RuntimeEntityChannel>(),
			p.GetRequiredService<TradeChannel>(),
			p.GetRequiredService<SpeechChannel>(),
			p.GetRequiredService<ChatChannel>(),
			p.GetRequiredService<LocationPingChannel>(),
			p.GetService<INativeWorldFacts>(),
			p.GetRequiredService<ItemKernelAuthority>(),
			p.GetRequiredService<IWorldItemLayerReset>(),
			p.GetRequiredService<FluidKernelProjection>(),
			p.GetRequiredService<FluidKernelReadProjection>(),
			p.GetRequiredService<ProjectionHealthCoordinator>()));
		services.AddSingleton<IWorldControl>(p => p.GetRequiredService<WorldService>());
		// The Runtime world-fact port the save system reads and rewrites (the block
		// diff and the radiation line) is the facade's own lifecycle, so a cut sees
		// exactly the tables the live world does. The partial block damage is NOT
		// part of it: it lives in the game's own list, behind INativeWorldFacts.
		services.AddSingleton<IWorldFactSource>(p => p.GetRequiredService<WorldService>());
		// The world-entry backfill fan-out owns the ordered snapshot group +
		// completion marker; it is injected into the handshake/scene handlers
		// so HandlerContext no longer owns a concrete world-entry flow.
		services.AddSingleton<WorldEntryFanout>();
		// Pending-report fallback pump: the single time edge for the guest's
		// unacknowledged block mutations (W1) and runtime entity creations (E3),
		// re-reported until the host answers. Both domains stay reaction-only;
		// this tiny pump is their clock.
		services.AddSingleton<WorldReportFallbackPump>();
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<WorldReportFallbackPump>());
		// Guest readiness convergence (sync-coverage rows R3/R4): the bounded absolute
		// scene re-report window that heals a swallowed InWorld report or start-gate
		// release, and the only production consumer of the world-entry completion
		// marker. Session control is a different family from the world-report
		// fallbacks above (a short bounded window versus an unbounded pending table),
		// so it is its own time edge.
		services.AddSingleton<SessionControlConvergence>();
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<SessionControlConvergence>());
	}
}
