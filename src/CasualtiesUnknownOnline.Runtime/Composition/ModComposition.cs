using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Mods;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Composition;

/// <summary>
/// The mod framework (Phase 4 Mod API): the discovery registry, the message
/// channel, the lifecycle coordinator, the status store and its projection read
/// model, the disabled default implementations of every native seam the Game
/// Adapter replaces, the building-runtime hook table, and the generic content
/// binder that runs after discovery. It registers after the item domain, whose
/// kernel seams mods reach through the item service.
/// </summary>
internal static class ModComposition
{
	/// <summary>Registers the mod registry, lifecycle, status projection, native-seam defaults and the content binder.</summary>
	internal static void AddMods(IServiceCollection services, string? modStateFile)
	{
		// Discovery registry (pure), the message channel and the coordinator (an
		// ICuoService — registered after the session it reads; the session's
		// IModListProvider resolves the registry lazily, so this order is safe).
		// The mod-state disk store is a persistence mechanism only (no pump); a
		// null path keeps it in-memory (the test composition default).
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
		// Registry-backed local mod-status projection read model: the same
		// store projected into the global ProjectionHealthCoordinator contract.
		// It is an ICuoService only to refresh when Steam/local identity changes
		// after late Steam initialization; it has no other pump work.
		services.AddSingleton<ModStatusProjectionReadModel>();
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<ModStatusProjectionReadModel>());
		// The native seams default to disabled: the Game Adapter is registered by
		// the plugin through extraRegistrations and replaces each one with the real
		// implementation (Utils.Create, WorldGeneration.SetBlock, FluidManager...),
		// since only that layer knows the game-private operations. Tests may also
		// replace them with recording fakes.
		services.AddSingleton<IModEntitySpawner>(new DisabledModEntitySpawner());
		services.AddSingleton<IModItemSpawner>(new DisabledModItemSpawner());
		services.AddSingleton<IModTilePlacer>(new DisabledModTilePlacer());
		services.AddSingleton<IModStructurePlacer>(new DisabledModStructurePlacer());
		services.AddSingleton<IModLiquidPlacer>(new DisabledModLiquidPlacer());
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
	}
}
