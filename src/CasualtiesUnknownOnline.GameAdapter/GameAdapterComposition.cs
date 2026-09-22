using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.GameAdapter.Content;
using CasualtiesUnknownOnline.GameAdapter.World;
using CasualtiesUnknownOnline.Runtime.Configuration;
using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Persistence;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Content;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using CasualtiesUnknownOnline.Runtime.Session.Persistence;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CasualtiesUnknownOnline.GameAdapter;

/// <summary>
/// The Game Adapter's composition: the one place that registers the adapter
/// singleton, its capability ports and every service the adapter owns. The
/// container itself is BUILT in the Runtime, which must not reference this
/// project, and the plugin owns the BepInEx lifecycle but not the adapter's
/// internals — this one call is its only reason to reference this project at
/// all — so the registration lives with the implementation it registers: the
/// host shell calls <see cref="Register"/> and never names an adapter type.
///
/// <para>
/// Registration order is the contract. The adapter registers first so the ports
/// resolve from the one singleton; the services that read those ports register
/// after it; the plugin's pump runs <c>ICuoService</c> in registration order,
/// which is why the content-binding providers sit after the adapter's own
/// install step and before nothing else depends on them.
/// </para>
/// </summary>
public static class GameAdapterComposition
{
	/// <summary>
	/// Registers the adapter and everything it owns. Called from the plugin's
	/// configuration pass, after the BepInEx config bridge (the options monitors
	/// these services resolve are registered there) and before the save-profile
	/// store that reads the same config file.
	/// </summary>
	public static void Register(IServiceCollection services)
	{
		// Character-data mapping (Mapster). Mapster 6.0.0 core ships
		// IMapper/Mapper — registered directly, no DI package needed
		// (Mapster.DependencyInjection 10.x requires net6+).
		services.AddSingleton<MapsterMapper.IMapper>(
			new MapsterMapper.Mapper(Mapster.TypeAdapterConfig.GlobalSettings));
		services.AddSingleton<GameAdapter>();
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<GameAdapter>());
		// The capability ports (review/adapter-capability-ports.md): a consumer
		// resolves the port whose capability it uses instead of the whole adapter,
		// and every port IS the one adapter singleton the implementation is.
		// The list is pinned against the aggregate's composition by
		// AdapterCapabilityPortShapeTests, so a new port cannot be left unwired.
		// `IGameAdapter` itself is deliberately NOT registered: the composition is
		// the seam's identity and its compile-time proof (the class declaration
		// implements all twelve ports), and nothing in the tree resolves the whole
		// adapter — a dead registration inside the surface this change narrows is
		// exactly the drift the shape gate exists to catch.
		services.AddSingleton<IGameIntegrationLifecycle>(p => p.GetRequiredService<GameAdapter>());
		services.AddSingleton<IAdapterCapabilityQuery>(p => p.GetRequiredService<GameAdapter>());
		services.AddSingleton<IWorldPresenceQuery>(p => p.GetRequiredService<GameAdapter>());
		services.AddSingleton<IStartGateState>(p => p.GetRequiredService<GameAdapter>());
		services.AddSingleton<ILocalHealItemQuery>(p => p.GetRequiredService<GameAdapter>());
		services.AddSingleton<ITraderRecruitRequest>(p => p.GetRequiredService<GameAdapter>());
		services.AddSingleton<INativeInputBlocker>(p => p.GetRequiredService<GameAdapter>());
		services.AddSingleton<IRemoteInventoryPresentation>(p => p.GetRequiredService<GameAdapter>());
		services.AddSingleton<IRemoteMedicalPresentation>(p => p.GetRequiredService<GameAdapter>());
		services.AddSingleton<IPlayerAnchorQuery>(p => p.GetRequiredService<GameAdapter>());
		services.AddSingleton<IJoinFlowPresentation>(p => p.GetRequiredService<GameAdapter>());
		services.AddSingleton<ICarryPresentationPump>(p => p.GetRequiredService<GameAdapter>());
		// The world library (decision 198): the worlds and backups the Online UI's Worlds page
		// manages, and the restore that replaces one world's live snapshot with an archive the
		// player picked. It is an ICuoService because an armed restore runs at a frame boundary
		// (the click only records the intent — see IWorldLibrary), and it is registered AFTER the
		// adapter so the "is a world loaded" fact read at that boundary is this frame's.
		services.AddSingleton(p => new WorldLibraryService(
			p.GetService<WorldRepository>(),
			p.GetRequiredService<IWorldSaveControl>(),
			p.GetRequiredService<ISessionControl>(),
			p.GetRequiredService<ILogger<WorldLibraryService>>(),
			p.GetService<IOptionsMonitor<SaveOptions>>(),
			worldActive: () => p.GetRequiredService<IWorldPresenceQuery>().IsInWorldOrGenerating));
		services.AddSingleton<IWorldLibrary>(p => p.GetRequiredService<WorldLibraryService>());
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<WorldLibraryService>());
		// The native world-fact reader/writer of the CUO world archive: the
		// adapter is the only layer that can read the game's keypad codes, geyser
		// liquid types and its own blockDamages list, so the save layer resolves
		// this port from here (it is OPTIONAL by design — a build without one
		// captures and restores only the Runtime-owned world facts and reports the
		// gap instead of defaulting silently).
		services.AddSingleton<NativeWorldFacts>();
		services.AddSingleton<INativeWorldFacts>(p => p.GetRequiredService<NativeWorldFacts>());
		// The host's member-facing trap-layout table is re-derived from the LIVE
		// scene by the Runtime's world-entry fan-out, right before every send:
		// only the adapter can scan the scene, so it implements the port for the
		// composition root (the Runtime resolves the port and cannot reference
		// the adapter). Optional by design — a build without it sends the table
		// as last derived.
		services.AddSingleton<LiveTrapLayoutSource>();
		services.AddSingleton<ILiveTrapLayoutSource>(p => p.GetRequiredService<LiveTrapLayoutSource>());
		services.Replace(ServiceDescriptor.Singleton<IModEntitySpawner>(p => p.GetRequiredService<GameAdapter>()));
		services.Replace(ServiceDescriptor.Singleton<IModItemSpawner>(p => p.GetRequiredService<GameAdapter>()));
		services.Replace(ServiceDescriptor.Singleton<IModTilePlacer>(p => p.GetRequiredService<GameAdapter>()));
		services.Replace(ServiceDescriptor.Singleton<IModStructurePlacer>(p => p.GetRequiredService<GameAdapter>()));
		services.Replace(ServiceDescriptor.Singleton<IModLiquidPlacer>(p => p.GetRequiredService<GameAdapter>()));
		services.Replace(ServiceDescriptor.Singleton<IModNativeApiProvider>(p => p.GetRequiredService<GameAdapter>()));
		// Item content binding: the runtime binder routes ModContentKind.Item
		// definitions to this Game Adapter provider. It runs after ModService in
		// the ICuoService pump so the first-frame discovery has already loaded the
		// mods and their content registrations.
		services.AddSingleton<GameAdapterItemContentProvider>();
		services.AddSingleton<IContentBindingProvider>(p => p.GetRequiredService<GameAdapterItemContentProvider>());
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<GameAdapterItemContentProvider>());
		// Vanilla content ids: the game's own item table becomes cu:<item id>
		// with the game-localised display name, feeding the console's
		// resource-location completion (the Runtime catalog aggregates it).
		services.AddSingleton<VanillaItemResourceLocationSource>();
		services.AddSingleton<IResourceLocationSource>(p => p.GetRequiredService<VanillaItemResourceLocationSource>());
		// Recipe content binding: the same generic binder routes
		// ModContentKind.Recipe definitions to this Game Adapter provider.
		services.AddSingleton<GameAdapterRecipeContentProvider>();
		services.AddSingleton<IContentBindingProvider>(p => p.GetRequiredService<GameAdapterRecipeContentProvider>());
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<GameAdapterRecipeContentProvider>());
		// Liquid content binding: typed static liquid DTOs into Liquids.Registry.
		services.AddSingleton<GameAdapterLiquidContentProvider>();
		services.AddSingleton<IContentBindingProvider>(p => p.GetRequiredService<GameAdapterLiquidContentProvider>());
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<GameAdapterLiquidContentProvider>());
		// Liquid-tile content binding: typed static world-fluid DTOs into the
		// vanilla FluidManager grid and local GameAdapter projection seams.
		services.AddSingleton<GameAdapterLiquidTileContentProvider>();
		services.AddSingleton<IContentBindingProvider>(p => p.GetRequiredService<GameAdapterLiquidTileContentProvider>());
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<GameAdapterLiquidTileContentProvider>());
		// Building content binding: typed building DTOs build runtime
		// BuildingEntity templates and feed the existing EntitySpawned channel.
		services.AddSingleton<GameAdapterBuildingContentProvider>();
		services.AddSingleton<IContentBindingProvider>(p => p.GetRequiredService<GameAdapterBuildingContentProvider>());
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<GameAdapterBuildingContentProvider>());
		// Tile content binding: typed static tile DTOs are injected into the
		// vanilla WorldGeneration.tiles palette and GetBlockInfo lookup.
		services.AddSingleton<GameAdapterTileContentProvider>();
		services.AddSingleton<IContentBindingProvider>(p => p.GetRequiredService<GameAdapterTileContentProvider>());
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<GameAdapterTileContentProvider>());
		// Structure content binding: typed static structure DTOs are stored in a
		// GameAdapter registry and served through the mod structure placement
		// seam; automatic worldgen distribution runs from the same registry.
		services.AddSingleton<GameAdapterStructureContentProvider>();
		services.AddSingleton<IContentBindingProvider>(p => p.GetRequiredService<GameAdapterStructureContentProvider>());
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<GameAdapterStructureContentProvider>());
		// Status content binding: typed static status descriptors are validated
		// and stored as migration base. Per-player/per-limb runtime values are
		// intentionally not part of this seam.
		services.AddSingleton<GameAdapterStatusContentProvider>();
		services.AddSingleton<IContentBindingProvider>(p => p.GetRequiredService<GameAdapterStatusContentProvider>());
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<GameAdapterStatusContentProvider>());
		// Moodle content binding: typed static moodle/presentation descriptors
		// are validated and stored; feeding the vanilla moodle manager remains
		// a future local UI/GameAdapter seam.
		services.AddSingleton<GameAdapterMoodleContentProvider>();
		services.AddSingleton<IContentBindingProvider>(p => p.GetRequiredService<GameAdapterMoodleContentProvider>());
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<GameAdapterMoodleContentProvider>());
		// The game-backed line-of-sight oracle is a standalone lightweight
		// GameAdapter service. It must NOT be registered through GameAdapter:
		// GameAdapter depends on IPlayerInteractionControl, and
		// PlayerInteractionService depends on IPlayerInteractionVisibility — routing
		// visibility through the adapter would create a DI constructor cycle.
		services.AddSingleton<PlayerInteractionVisibility>();
		services.Replace(ServiceDescriptor.Singleton<IPlayerInteractionVisibility>(
			p => p.GetRequiredService<PlayerInteractionVisibility>()));
		// The live local-body capture the target-body verdict seam asks for. Same
		// standalone-service shape and the same cycle reason as the visibility oracle
		// above: it must not be reached through GameAdapter.
		services.AddSingleton<LocalCharacterCapture>();
		services.Replace(ServiceDescriptor.Singleton<ILocalCharacterCapture>(
			p => p.GetRequiredService<LocalCharacterCapture>()));
	}
}
