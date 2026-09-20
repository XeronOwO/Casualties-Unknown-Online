using Microsoft.Extensions.DependencyInjection;

namespace CasualtiesUnknownOnline.Runtime.Session.Content;

/// <summary>
/// The content-vocabulary block of the composition root: the built-in and
/// mod-content resource sources, the catalog the console completes resource
/// arguments from, and the extra completion stages (pinyin today). It lives in
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
		services.AddSingleton<ResourceLocationCatalog>();
		services.AddSingleton<IResourceLocationCatalog>(p => p.GetRequiredService<ResourceLocationCatalog>());
		// Pinyin resource completion is an extra match stage, so the switch-off
		// path is "the stage contributes nothing" rather than a special case in
		// the catalog. The stage reads Search.PinyinSearch live and loads no
		// reading table while it is off.
		services.AddSingleton<IResourceLocationMatchStage, PinyinResourceLocationMatchStage>();
	}
}
