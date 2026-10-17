using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace CasualtiesUnknownOnline.Runtime.Composition;

/// <summary>
/// The in-game command console: the local slash-command chain, the chat UI surface
/// it rides, and the Unity-free input state machine (history, completion cycling,
/// ESC/Enter) the standalone overlay shares. It sends no wire message and owns no
/// packet handler — the chat path is its only transport — so it registers as one
/// local surface, after the content vocabulary whose catalog its resource
/// arguments complete from.
/// </summary>
internal static class ConsoleComposition
{
	/// <summary>Registers the command registry, the console service and its input session.</summary>
	internal static void AddConsole(IServiceCollection services)
	{
		// The acceptance verbs register themselves through the same attribute scan the other owners use, so they have to
		// be CONSTRUCTED for the run to see them — an unrequested singleton is never created. Building them here, while
		// the registry is being built, is what makes that happen without the console service carrying a member for it.
		services.AddSingleton(p =>
		{
			var registry = new ConsoleCommandRegistry();
			_ = ActivatorUtilities.CreateInstance<AcceptanceCommands>(p, registry);
			return registry;
		});
		// The local scene the item-data write verb reads and writes. The Game Adapter
		// REPLACES this with the real one where an adapter is loaded; without it the
		// verb answers "this client holds no such item" instead of pretending the
		// scene is empty, which is the same shape ILocalCharacterCapture uses.
		services.AddSingleton<ILocalItemAccess>(new UnavailableLocalItemAccess());
		services.AddSingleton<CommandConsoleService>();
		services.AddSingleton<ICommandControl>(p => p.GetRequiredService<CommandConsoleService>());
		services.AddSingleton<ICommandCompletionSource>(p => p.GetRequiredService<CommandConsoleService>());
		services.AddSingleton<ICommandArgumentSuggestions>(p => p.GetRequiredService<CommandConsoleService>());
		services.AddSingleton<ConsoleInputSession>();
	}
}
