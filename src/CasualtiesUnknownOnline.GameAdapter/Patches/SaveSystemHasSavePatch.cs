using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Decision 165: CUO never reads the native <c>save.sv</c>. The game's own menu
/// asks <see cref="SaveSystem.HasSave"/> and then, when it answers true, PARSES
/// the file for the Continue button's icon and played-time label
/// (PreRunScript.cs:65-89) — so leaving that reader alive would mean CUO still
/// opens <c>save.sv</c>, and a corrupt native save would raise the game's own
/// "could not be parsed" alert on a Continue entry CUO owns.
///
/// The prefix answers "no native save" while CUO is bound: the menu's own read
/// is skipped, and the Continue entry's reachability is then set from the CUO
/// world repository by <see cref="PreRunScriptStartPatch"/>.
/// </summary>
[HarmonyPatch(typeof(SaveSystem), "HasSave")]
internal static class SaveSystemHasSavePatch
{
	private static bool Prefix(ref bool __result)
	{
		if (PatchBridge.Impl is null)
		{
			return true;
		}

		__result = false;
		return false;
	}
}
