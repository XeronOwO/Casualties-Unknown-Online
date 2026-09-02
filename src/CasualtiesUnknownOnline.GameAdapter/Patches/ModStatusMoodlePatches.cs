using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Mod-status vanilla moodle-row hooks (phase 3 local UI seam). The moodle
/// manager clears and rebuilds its rows every ~0.5 s; important (main-row)
/// mod moodles are added in the <c>AddAllMoodles</c> prefix before the native
/// method flips <c>sideMoodles</c>, so they appear in the main row. The
/// postfix adds non-important mod moodles after the native side moodles.
/// </summary>
internal static class ModStatusMoodlePatches
{
	[HarmonyPatch(typeof(MoodleManager), "AddAllMoodles")]
	internal static class ModMoodlePatch
	{
		private static void Prefix(MoodleManager __instance) =>
			PatchBridge.Impl?.ApplyModMoodles(__instance, importantRow: true);

		private static void Postfix(MoodleManager __instance) =>
			PatchBridge.Impl?.ApplyModMoodles(__instance, importantRow: false);
	}
}
