using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.RecursiveCrafting;

/// <summary>
/// The log sink the static command class reads. The CUO mod entry binds it from the mod-scoped logger at
/// <see cref="ICuoMod.Bind"/> — NOT the BepInEx plug-in shell, which BepInEx need not load for a CUO-only mod
/// (measured: the assembly is discovered straight off the plugins folder, so the shell's Awake never ran and a
/// sink bound there stayed null). A static handler cannot take constructor injection, so this is the same
/// discipline CUO's own patch gates use, with the binding moved to the surface that is actually instantiated.
/// </summary>
internal static class RecursiveCraftingGate
{
	internal static ILogger? Log { get; private set; }

	internal static void Bind(ILogger log) => Log = log;
}
