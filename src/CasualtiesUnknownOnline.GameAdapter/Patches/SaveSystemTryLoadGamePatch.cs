using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Decision 165: CUO never reads the native <c>save.sv</c>. A continue is
/// restored from the CUO world archive, and the game's own
/// <c>SaveSystem.TryLoadGame</c> — which parses <c>save.sv</c>, applies the body
/// by reflection and rewrites <c>biomeDepth</c> — must therefore never run.
///
/// The prefix does NOT touch <c>SaveSystem.loadedRun</c>: the game's own
/// <c>PreRunScript.LoadRun</c> set it, and the rest of the game reads it to
/// behave as a continuation (no fresh starting supplies). Only the native
/// payload application is skipped; every fact comes from the CUO restore.
///
/// The skipped call is also the slot the RESTORED RUN FIELDS go through. The
/// caller is <c>WorldGeneration.Start</c>, and the very next statements derive
/// the layer's time limit and trap budget from exactly those values
/// (<c>WorldGeneration.cs:252-262</c>), so this is the last moment they can be
/// written — and the world object exists here, which it does not at the Continue
/// click.
/// </summary>
[HarmonyPatch(typeof(SaveSystem), "TryLoadGame")]
internal static class SaveSystemTryLoadGamePatch
{
	private static bool Prefix()
	{
		var bridge = PatchBridge.Impl;
		if (bridge is null)
		{
			return true; // no CUO in this process: the game loads its own save.sv
		}

		bridge.OnNativeSaveSlot();
		return false;
	}
}
