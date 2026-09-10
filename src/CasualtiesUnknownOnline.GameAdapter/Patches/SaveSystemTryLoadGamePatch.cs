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
/// </summary>
[HarmonyPatch(typeof(SaveSystem), "TryLoadGame")]
internal static class SaveSystemTryLoadGamePatch
{
	private static bool Prefix() => PatchBridge.Impl is null;
}
