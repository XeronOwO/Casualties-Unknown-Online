using CasualtiesUnknownOnline.Abstractions;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.RecursiveCrafting;

/// <summary>
/// The CUO half of the mod: the entry CUO discovers in this assembly. It installs the recipe panel's third state
/// (the Harmony patch on the row's name and tooltip) and registers the local console command that makes the
/// resolver explainable — the panel shows the marker, the command says which steps. It is local-only
/// (<see cref="NetworkMode.ClientOnly"/>): nothing here changes the session, so a client and a host may run
/// different versions without moving the wire.
/// </summary>
[CuoMod("cuo.recursivecrafting", "Recursive Crafting", "0.1.0", NetworkMode = NetworkMode.ClientOnly,
	Permissions = ModPermission.RegisterLocalCommand,
	NativeBinding = "Recipe.fullName + Recipe.description getters",
	Description = "Marks a recipe reachable through other crafts in the game's own recipe panel, with the steps "
		+ "in its tooltip, and answers /craftchain on the local console with the full chain or the reason.")]
public sealed class RecursiveCraftingMod : ICuoMod
{
	/// <inheritdoc />
	public void Bind(IModContext context)
	{
		RecursiveCraftingGate.Bind(context.Logger);
		if (CraftingPanelInstaller.Ensure())
		{
			context.Logger.LogInformation("[Recursive] recipe-panel third state installed (name marker + tooltip steps).");
		}
		if (context.ConsoleCommands.Register(new ConsoleCommandDefinition(
			"craftchain",
			"Show how a recipe is reachable (now, through steps, or why not)",
			RecursiveCraftingCommand.Execute,
			parameters: [new ConsoleCommandParameter("itemId")],
			usage: "/craftchain <itemId>")))
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
