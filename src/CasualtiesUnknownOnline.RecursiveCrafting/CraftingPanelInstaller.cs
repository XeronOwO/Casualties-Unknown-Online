using System;
using System.Linq;
using HarmonyLib;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.RecursiveCrafting;

/// <summary>
/// Applies the mod's Harmony patches exactly once, whichever entry runs first: the BepInEx plug-in shell when a
/// client loads it as a package, or the CUO mod entry when CUO discovers the assembly by itself (the shell's
/// Awake may never run in that case — measured). Idempotent so both paths can call it.
/// </summary>
internal static class CraftingPanelInstaller
{
	private const string HarmonyId = "CasualtiesUnknownOnline.RecursiveCrafting";

	private static bool _applied;

	/// <summary>Patch the recipe panel's two surfaces. Returns false when the patch set could not be applied.</summary>
	internal static bool Ensure()
	{
		if (_applied)
		{
			return true;
		}

		try
		{
			var harmony = new Harmony(HarmonyId);
			// Patch the ASSEMBLY, not the outer class: HarmonyX's PatchAll(Type) does not descend into nested
			// patch classes on this runtime (measured: it reported success while Recipe.get_fullName stayed
			// unpatched), and the assembly holds nothing but this mod's own patches.
			harmony.PatchAll(typeof(CraftingPanelInstaller).Assembly);
			_applied = true;
			RecursiveCraftingGate.Log?.LogInformation(
				$"[Recursive] {harmony.GetPatchedMethods().Count()} Harmony target(s) patched by this mod.");
			return true;
		}
		catch (Exception exception)
		{
			RecursiveCraftingGate.Log?.LogWarning(
				$"[Recursive] the recipe-panel patches failed: {exception.GetType().Name}: {exception.Message}");
			return false;
		}
	}
}
