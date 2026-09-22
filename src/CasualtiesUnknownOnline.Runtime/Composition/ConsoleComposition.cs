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
		services.AddSingleton<ConsoleCommandRegistry>();
		services.AddSingleton<CommandConsoleService>();
		services.AddSingleton<ICommandControl>(p => p.GetRequiredService<CommandConsoleService>());
		services.AddSingleton<ICommandCompletionSource>(p => p.GetRequiredService<CommandConsoleService>());
		services.AddSingleton<ICommandArgumentSuggestions>(p => p.GetRequiredService<CommandConsoleService>());
		services.AddSingleton<ConsoleInputSession>();
	}
}
