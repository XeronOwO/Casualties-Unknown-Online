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
		// Item domain: the authoritative world-item table + pickup arbitration
		// (ItemService itself reacts to calls and messages — no pump).
		services.AddSingleton<ItemService>();
		services.AddSingleton<IItemControl>(p => p.GetRequiredService<ItemService>());
		// The item domain's layer-boundary reset, driven by the world domain's
		// per-layer reset bus (WorldService.ResetWorldLayerTables).
		services.AddSingleton<IWorldItemLayerReset>(p => p.GetRequiredService<ItemService>());
	}
}
