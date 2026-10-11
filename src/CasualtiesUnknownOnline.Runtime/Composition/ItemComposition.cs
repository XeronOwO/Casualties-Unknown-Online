using CasualtiesUnknownOnline.Abstractions;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using CasualtiesUnknownOnline.Runtime.Session.ProjectionHealth;
using Microsoft.Extensions.DependencyInjection;

namespace CasualtiesUnknownOnline.Runtime.Composition;

/// <summary>
/// The item domain: the authoritative world-item table with its pickup arbitration,
/// and the layer-boundary reset the world domain drives. It has two entry points
/// because the kernel replication block registers between them — the kernel
/// authority is what the item service executes against, and the update order the
/// plugin receives is the registration order, so the blocks stay exactly where they
/// stood inline rather than being gathered for tidiness.
///
/// <para>
/// <see cref="AddItemTables"/> also carries the global projection-health
/// coordinator: it is the observation contract the world, item and mod read models
/// project into, and it registered with this domain's tables.
/// </para>
/// </summary>
internal static class ItemComposition
{
	/// <summary>Registers the projection-health coordinator and the item arbitration table.</summary>
	internal static void AddItemTables(IServiceCollection services)
	{
		services.AddSingleton<ProjectionHealthCoordinator>();
		services.AddSingleton<ICuoService>(p => p.GetRequiredService<ProjectionHealthCoordinator>());
		// ItemArbitration is DI-registered so the crafting domain composes the
		// same transfer table (RemoveTransferred/AdoptEvidence/RegisterCarried).
		services.AddSingleton<ItemArbitration>();
	}

	/// <summary>Registers the item service and the item control/layer-reset ports it answers.</summary>
	internal static void AddItemService(IServiceCollection services)
	{
		// The id coordination is its OWN registration, not an implementation detail of
		// ItemService: the per-space watermarks it owns are the resume points a handshake grant
		// and a restore both write, and the save layer reaches the same table through
		// IRestoredItemIdSeed — a second counter anywhere would re-issue ids the world holds.
		// There is ONE namespace: the synthesized one the medical removal effect used to mint
		// from went with its only consumer, because an applied item keeps its id across the
		// transform.
		services.AddSingleton<ItemIdCoordinator>();
		// Item domain: the authoritative world-item table + pickup arbitration
		// (ItemService itself reacts to calls and messages — no pump).
		services.AddSingleton<ItemService>();
		services.AddSingleton<IItemControl>(p => p.GetRequiredService<ItemService>());
		// The id seed a restore hands the archive's own ids to (the item domain's half of
		// the pair with IRestoredWorldItemSource): the save layer reaches the resume rule
		// through this port, never through the handler surface behind it.
		services.AddSingleton<IRestoredItemIdSeed>(p => p.GetRequiredService<ItemService>());
		// The item domain's layer-boundary reset, driven by the world domain's
		// per-layer reset bus (WorldService.ResetWorldLayerTables).
		services.AddSingleton<IWorldItemLayerReset>(p => p.GetRequiredService<ItemService>());
	}
}
