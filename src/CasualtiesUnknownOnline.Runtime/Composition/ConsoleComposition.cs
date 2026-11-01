using CasualtiesUnknownOnline.Runtime.GameAdapter;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Commands;
using CasualtiesUnknownOnline.Runtime.Session.EntitySync;
using CasualtiesUnknownOnline.Runtime.Session.World;
using CasualtiesUnknownOnline.Runtime.Session.PlayerInteraction;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
		// The local scene the item-data write verb reads and writes. The Game Adapter
		// REPLACES this with the real one where an adapter is loaded; without it the
		// verb answers "this client holds no such item" instead of pretending the
		// scene is empty, which is the same shape ILocalCharacterCapture uses.
		services.AddSingleton<ILocalItemAccess>(new UnavailableLocalItemAccess());
		// The item state language's schema and projection. The Game Adapter REPLACES this
		// with the real one; without it the verbs answer that they cannot name the item's
		// state rather than printing a shape no item in this build carries.
		services.AddSingleton<IItemStateLanguage>(new UnavailableItemStateLanguage());
		// The one scene capability behind the console /summon verb: the native position
		// words and one object summoned in the world. The Game Adapter REPLACES this
		// with the real one; without it the verb answers that this build cannot create
		// world objects instead of creating nothing and saying nothing.
		services.AddSingleton<IWorldSummon>(new UnavailableWorldSummon());
		// The one scene capability behind the console /heal cheat verb: the full reset of THIS
		// client's own body, with its severed-limb restore and hollow clear. The Game Adapter
		// REPLACES this with the real one; without it the verb answers that this build cannot reset a
		// body instead of resetting nothing and saying nothing.
		services.AddSingleton<ILocalBodyReset>(new UnavailableLocalBodyReset());
		// The one scene capability behind the console /kill cheat verb: zero brain health on THIS
		// client's own body, which is the whole of the game's own kill verb. The Game Adapter
		// REPLACES this with the real one; without it the verb answers that this build cannot kill a
		// body instead of killing nothing and saying nothing.
		services.AddSingleton<ILocalBodyKill>(new UnavailableLocalBodyKill());
		services.AddSingleton<CommandRelayHub>();
		// The verb groups, registered as themselves. The console asks for
		// IEnumerable<IConsoleCommandGroup> and runs its attribute scan once per element, so this block
		// is the ONLY place a new family is announced — the console carries no list.
		// (docs/development/agent-reference.md: attribute + reflection registration over hard-coded
		// linear registration for large verb families.)
		// The verb groups log through the console's own category (a non-generic ILogger), so the container
		// bridges it once here instead of every group carrying a factory.
		services.AddSingleton<ILogger>(p => p.GetRequiredService<ILogger<CommandConsoleService>>());
		services.AddSingleton<CheatVerbGate>();
		services.AddSingleton<ConsoleCommandAuthority>();
		services.AddSingleton<ItemDataTargets>();
		services.AddSingleton<IConsoleCommandGroup, HostAdminCommands>();
		services.AddSingleton<IConsoleCommandGroup, CheatAdminCommands>();
		// Two groups take the spectator surface, which a build without a game scene does not register: the
		// factories pass null, which those groups already treat as "spectating unavailable".
		services.AddSingleton<IConsoleCommandGroup>(p => new SpectatorCommands(
			p.GetRequiredService<ISessionControl>(),
			p.GetRequiredService<IEntitySyncControl>(),
			p.GetService<ISpectatorControl>(),
			p.GetRequiredService<ILogger>()));
		services.AddSingleton<IConsoleCommandGroup>(p => new GamemodeCommands(
			p.GetService<ISpectatorControl>(),
			p.GetRequiredService<ILogger>()));
		services.AddSingleton<IConsoleCommandGroup, WorldSaveCommands>();
		services.AddSingleton<IConsoleCommandGroup, HostRuleCommands>();
		// The write group is a dependency of the /data group as well as a verb group of its own, so it is
		// registered as itself and the group registration points at the same instance.
		services.AddSingleton<ItemDataWriteCommands>();
		services.AddSingleton<IConsoleCommandGroup>(p => p.GetRequiredService<ItemDataWriteCommands>());
		services.AddSingleton<IConsoleCommandGroup, ItemDataCommands>();
		services.AddSingleton<IConsoleCommandGroup, ItemGiveCommands>();
		services.AddSingleton<IConsoleCommandGroup, WorldSummonCommands>();
		services.AddSingleton<IConsoleCommandGroup, HealResetCommands>();
		services.AddSingleton<IConsoleCommandGroup, KillCommands>();
		services.AddSingleton<IConsoleCommandGroup, AcceptanceCommands>();
		services.AddSingleton<CommandConsoleService>();
		services.AddSingleton<ICommandControl>(p => p.GetRequiredService<CommandConsoleService>());
		services.AddSingleton<ICommandCompletionSource>(p => p.GetRequiredService<CommandConsoleService>());
		services.AddSingleton<ICommandArgumentSuggestions>(p => p.GetRequiredService<CommandConsoleService>());
		services.AddSingleton<ConsoleInputSession>();
	}
}
