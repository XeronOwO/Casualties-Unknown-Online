using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Application.Kernel;
using CasualtiesUnknownOnline.GameAdapter.Content;
using CasualtiesUnknownOnline.GameAdapter.Items;
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
		// The item domain's ONE id authority (ids are (counter, account id)): the aggregate's
		// domains allocate through it and the console /give seam below allocates through the
		// same instance. It is the container's rather than the aggregate's so the seam can be
		// built without the aggregate — a second allocator would be a second counter, whose
		// ids the watermark, the peers' snapshots and the archive never saw.
		services.AddSingleton<ItemIdAllocator>();
		services.AddSingleton<GameAdapter>();
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<GameAdapter>());
		// The host runs the game's own vital stages on its per-player clones (stage 2 of the
		// health-authority ticket), so the live run settings are read inside the game's code; no
		// run-setting port is registered here.
		// The capability ports (done/adapter-capability-ports.md): a consumer
		// resolves the port whose capability it uses instead of the whole adapter,
		// and every port IS the one adapter singleton the implementation is.
		// The list is pinned against the aggregate's composition by
		// AdapterCapabilityPortShapeTests, so a new port cannot be left unwired.
		// `IGameAdapter` itself is deliberately NOT registered: the composition is
		// the seam's identity and its compile-time proof (the class declaration
		// implements all seventeen ports), and nothing in the tree resolves the whole
		// adapter — a dead registration inside the surface this change narrows is
		// exactly the drift the shape gate exists to catch.
		services.AddSingleton<IGameIntegrationLifecycle>(p => p.GetRequiredService<GameAdapter>());
		services.AddSingleton<IAdapterCapabilityQuery>(p => p.GetRequiredService<GameAdapter>());
		services.AddSingleton<IWorldPresenceQuery>(p => p.GetRequiredService<GameAdapter>());
		services.AddSingleton<IStartGateState>(p => p.GetRequiredService<GameAdapter>());
		services.AddSingleton<IDragUseCueState>(p => p.GetRequiredService<GameAdapter>());
		services.AddSingleton<ILocalHealItemQuery>(p => p.GetRequiredService<GameAdapter>());
		services.AddSingleton<ITraderRecruitRequest>(p => p.GetRequiredService<GameAdapter>());
		services.AddSingleton<INativeInputBlocker>(p => p.GetRequiredService<GameAdapter>());
		services.AddSingleton<ISpectatorControl>(p => p.GetRequiredService<GameAdapter>());
		services.AddSingleton<IRemoteInventoryPresentation>(p => p.GetRequiredService<GameAdapter>());
		services.AddSingleton<IRemoteMedicalPresentation>(p => p.GetRequiredService<GameAdapter>());
		services.AddSingleton<IPlayerAnchorQuery>(p => p.GetRequiredService<GameAdapter>());
		services.AddSingleton<IJoinFlowPresentation>(p => p.GetRequiredService<GameAdapter>());
		services.AddSingleton<ICarryPresentationPump>(p => p.GetRequiredService<GameAdapter>());
		services.AddSingleton<IOnlineUiNativeFactsQuery>(p => p.GetRequiredService<GameAdapter>());
		// The Online UI's live surface (S2a): the adapter owns CUO's canvas under the game's canvas and
		// the game's own launcher control on it, and the plugin drives it with frames over this port.
		services.AddSingleton<IOnlineUiSurface>(p => p.GetRequiredService<GameAdapter>());
		// The console /heal cheat verb's body half: the full reset of the local body. A port on the
		// aggregate rather than a standalone seam because the reset must re-report the character
		// snapshot, and the capture/report live on the character domain the aggregate owns. No
		// constructor cycle: nothing in the aggregate's own graph consumes this port (the Runtime's
		// heal service does, and the aggregate does not depend on it). REPLACES the Runtime's
		// UnavailableLocalBodyReset, like every other Runtime-seeded scene seam: two descriptors
		// would leave the real one winning on registration order alone, and a later reorder would
		// silently make the deployed verb answer "this build has no game scene".
		services.Replace(ServiceDescriptor.Singleton<ILocalBodyReset>(
			p => p.GetRequiredService<GameAdapter>()));
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
		// mods and their content registrations. The provider installs the two
		// native delegates a mod item carries; they call its ItemActionInvoker,
		// which resolves the action the item's OWNER registered through
		// IModItemActions, on the client the gesture was performed on.
		services.AddSingleton<GameAdapterItemContentProvider>();
		services.AddSingleton<IContentBindingProvider>(p => p.GetRequiredService<GameAdapterItemContentProvider>());
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<GameAdapterItemContentProvider>());
		// The crafting-quality vocabulary the recipe provider validates a
		// quality-referencing ingredient against: the item and liquid providers
		// declare what their accepted definitions carry, and the recipe provider
		// asks every source before it falls back to the vanilla tables. Neither
		// registration is an ICuoService, so the pump order does not move.
		services.AddSingleton<ICraftingQualitySource>(p => p.GetRequiredService<GameAdapterItemContentProvider>());
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
		// The provider installs the two native delegates a mod liquid carries; they
		// call its LiquidEffectInvoker, which resolves the effect the liquid's OWNER
		// registered through IModLiquidEffects, on the affected player's own client.
		services.AddSingleton<GameAdapterLiquidContentProvider>();
		services.AddSingleton<IContentBindingProvider>(p => p.GetRequiredService<GameAdapterLiquidContentProvider>());
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<GameAdapterLiquidContentProvider>());
		services.AddSingleton<ICraftingQualitySource>(p => p.GetRequiredService<GameAdapterLiquidContentProvider>());
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
		// The console /give verb's creation half: the game's item table + prefabs and the
		// one instance-id allocator, exposed as the Runtime's narrow port. It is a
		// STANDALONE seam, never a port on the aggregate: PlayerInteractionService consumes
		// the port and GameAdapter depends on IPlayerInteractionControl (it subscribes to
		// the interaction events), so routing it through the adapter is a constructor
		// cycle — the same shape and the same reason as the visibility oracle above. The
		// seam needs nothing the domain graph owns: the item table and the prefab resolver
		// are the game's own statics and the allocator is the container's singleton.
		services.AddSingleton<ItemGrantSource>();
		services.Replace(ServiceDescriptor.Singleton<IItemGrantSource>(
			p => p.GetRequiredService<ItemGrantSource>()));
		// The container half of the give ladder: the same standalone seam shape and the same
		// cycle reason as the creation port above (the take service that consumes it is built
		// before the aggregate). It reads the prefab's serialized Container and the item
		// table's own weights, so it needs nothing the adapter's domain graph owns.
		services.AddSingleton<ItemContainerFit>();
		services.Replace(ServiceDescriptor.Singleton<IItemContainerFit>(
			p => p.GetRequiredService<ItemContainerFit>()));
		// The receiving client's placement answer: the same standalone seam shape and the same cycle
		// reason as the two seams above — the Runtime's placement service is built inside
		// PlayerInteractionService, which the aggregate's own constructor depends on. It reads the live
		// local body, the prefab's serialized rules and the item table, so it needs nothing the domain
		// graph owns, and it answers a QUESTION (it writes nothing on either side).
		services.AddSingleton<RemotePlacementResolver>();
		services.Replace(ServiceDescriptor.Singleton<IRemotePlacementResolver>(
			p => p.GetRequiredService<RemotePlacementResolver>()));
		// The world-summon seam behind the console /summon verb: the native command's position words and ONE
		// object summoned at a point (an item entity through the creation port above and the shared world-item
		// creation — the same the give ladder's drop makes — or an entity through Utils.Create). The console
		// builds it at startup, so it must not force the aggregate, whose own graph depends on the controls this
		// seam's neighbours live in. It takes the item control and the give creation port (itself a standalone
		// seam) plus the game's own statics (the prefab tables, the item table and the world instance), never the
		// aggregate or a service that reaches it.
		services.AddSingleton<WorldSummoner>();
		services.Replace(ServiceDescriptor.Singleton<IWorldSummon>(
			p => p.GetRequiredService<WorldSummoner>()));
		// The game's own line-of-sight oracle is a standalone lightweight
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
		// The local scene behind the console's item-data WRITE verb: the one object an
		// instance id names on this client, its captured state, and one write into it.
		// The Runtime registers the unavailable one and this replaces it, so a build
		// without an adapter answers "not held" instead of pretending an empty scene.
		// Standalone rather than a port on the aggregate: it needs nothing the domain
		// graph owns (the id index, the codec and the standing classifier are statics).
		services.AddSingleton<LocalItemAccess>();
		services.Replace(ServiceDescriptor.Singleton<ILocalItemAccess>(
			p => p.GetRequiredService<LocalItemAccess>()));
		// The item state language's adapter half: the document projection, the schema
		// completion asks for, and the legality the write/create ports bind against. It is
		// a pure mapping over the game's statics (component names, the liquid registry, the
		// prefab's capacity), so it is standalone for the same reason as the seam above.
		services.Replace(ServiceDescriptor.Singleton<IItemStateLanguage>(new ItemStateLanguage()));
		// The game's own item data behind the native-use bound: the admission seam asks
		// whether a definition's ItemInfo.usable admits a native use at all, which only
		// this layer can read. Standalone for the same reason as the semantics seams
		// below — it needs nothing the domain graph owns.
		services.AddSingleton<GameItemUseFacts>();
		services.Replace(ServiceDescriptor.Singleton<IKernelItemUseFacts>(
			p => p.GetRequiredService<GameItemUseFacts>()));
		// The game's own content facts behind the cross-player limb-use chains
		// (mod-cross-player-native-semantics): the item and liquid registries answer
		// what the injection chain may draw from, instead of a CUO id table. Same
		// standalone-service shape and the same cycle reason as the two seams above.
		services.AddSingleton<GameLimbUseSemantics>();
		services.Replace(ServiceDescriptor.Singleton<ILimbUseSemantics>(
			p => p.GetRequiredService<GameLimbUseSemantics>()));
		// The game's own content facts behind the consume (drink) chain, the same
		// shape for the family that has no limb: ItemInfo.usable on a
		// LiquidItemInfo is the gate the game's own use action runs behind.
		services.AddSingleton<GameConsumeSemantics>();
		services.Replace(ServiceDescriptor.Singleton<IConsumeSemantics>(
			p => p.GetRequiredService<GameConsumeSemantics>()));
		// The game's own content facts behind the wearable (wear) chain, the same
		// shape for the family that goes ON a body rather than into it:
		// ItemInfo.wearable is the flag the game's own wear flow gates on, and its
		// wearSlotId / desiredWearLimb are the placement that flow resolves.
		services.AddSingleton<GameWearSemantics>();
		services.Replace(ServiceDescriptor.Singleton<IWearSemantics>(
			p => p.GetRequiredService<GameWearSemantics>()));
		// The game's own content facts behind the SOLID-FOOD chain, the same shape
		// for the family whose behaviour is not data at all: the item's use action
		// is a delegate, and its compiled body is what says whether it feeds the
		// eating body (Body.Eat / Body.Drink) and what it does to the item object.
		services.AddSingleton<GameSolidFoodFacts>();
		services.Replace(ServiceDescriptor.Singleton<ISolidFoodSemantics>(
			p => p.GetRequiredService<GameSolidFoodFacts>()));
		// The engine random stream a shrapnel grip slip's derived window is taken from. The engine's
		// own stream is the only thing both machines can draw the same values from, and this adapter
		// is the only layer that may touch it (cross-player-item-use-random-determinism).
		services.Replace(ServiceDescriptor.Singleton<IShrapnelGripSlipRandom>(
			new EngineShrapnelGripSlipRandom()));
	}
}
