using CasualtiesUnknownOnline.Runtime.Session.Mods;
using Microsoft.Extensions.DependencyInjection;

namespace CasualtiesUnknownOnline.Runtime.Session.Content;

/// <summary>
/// The content-vocabulary block of the composition root: the built-in and
/// mod-content resource sources, the catalog the console completes resource
/// arguments from, and the store a mod's completion stages land in. It lives in
/// its own file so <c>CuoBootstrap</c> stays inside its architecture line cap;
/// the registrations and their order are unchanged from the block that was
/// there. The Game Adapter's vanilla game-content source joins the same list
/// from the plugin's extra registrations, because only that layer may read the
/// game's item table.
/// </summary>
internal static class ContentVocabularyComposition
{
	/// <summary>Registers the content-id vocabulary and its completion stages.</summary>
	internal static void AddContentVocabulary(IServiceCollection services)
	{
		services.AddSingleton<BuiltInResourceLocationSource>();
		services.AddSingleton<IResourceLocationSource>(p => p.GetRequiredService<BuiltInResourceLocationSource>());
		services.AddSingleton<ModContentResourceLocationSource>();
		services.AddSingleton<IResourceLocationSource>(p => p.GetRequiredService<ModContentResourceLocationSource>());
		// A mod's completion stages land in this store: the catalog reads it and
		// the per-mod adapter writes it. The store has no dependencies of its own,
		// and that is what keeps a mod-service → catalog edge out of the graph —
		// the catalog already reaches the mod service through its content source,
		// so a direct edge would close a dependency cycle.
		services.AddSingleton(p => new ModResourceCompletionStore());
		services.AddSingleton<ResourceLocationCatalog>();
		services.AddSingleton<IResourceLocationCatalog>(p => p.GetRequiredService<ResourceLocationCatalog>());
	}
}
