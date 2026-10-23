using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.RecursiveCrafting;

/// <summary>
/// The CUO half of the mod: the entry CUO discovers in this assembly, which registers the local console command
/// that makes the resolver usable — without it the Core's rules would have no reader at all (the ticket's panel
/// state is a later stage). It is local-only (<see cref="NetworkMode.ClientOnly"/>): nothing here changes the
/// session, so a client and a host may run different versions without moving the wire.
/// </summary>
[CuoMod("cuo.recursivecrafting", "Recursive Crafting", "0.1.0", NetworkMode = NetworkMode.ClientOnly,
	Permissions = ModPermission.RegisterLocalCommand,
	Description = "Answers whether a recipe is reachable through other crafts, and through which steps, on the "
		+ "local console (/craftchain). The recipe panel's third state is a later stage of the same ticket.")]
public sealed class RecursiveCraftingMod : ICuoMod
{
	/// <inheritdoc />
	public void Bind(IModContext context)
	{
		RecursiveCraftingGate.Bind(context.Logger);
		if (context.ConsoleCommands.Register(new ModConsoleCommand(
			"craftchain",
			"Show how a recipe is reachable (now, through steps, or why not)",
			"/craftchain <itemId>",
			CommandPermission.Anyone,
			[CommandArgumentKind.Text],
			RecursiveCraftingCommand.Execute)))
		{
			context.Logger.LogInformation("[Recursive] console command /craftchain registered.");
			return;
		}

		context.Logger.LogWarning("[Recursive] the console refused the /craftchain registration; the mod has no active surface.");
	}

	/// <inheritdoc />
	public void Initialize() => RecursiveCraftingGate.Log?.LogInformation("[Recursive] mod entry initialized.");

	/// <inheritdoc />
	public void Start()
	{
	}

	/// <inheritdoc />
	public void Update()
	{
	}

	/// <inheritdoc />
	public void Stop()
	{
	}

	/// <inheritdoc />
	public void Dispose()
	{
	}
}
