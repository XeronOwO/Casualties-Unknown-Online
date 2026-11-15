using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// A session win is not the solo progression. <c>WorldGeneration.SetGameWon</c>
/// writes <c>wonchar&lt;charType&gt;</c> and unlocks the character achievement;
/// while a session is active the native body is skipped, so the solo picker's
/// store is byte-identical after the session. Outside a session the native
/// write runs unchanged.
/// </summary>
[HarmonyPatch(typeof(WorldGeneration), "SetGameWon")]
internal static class WorldGenerationSetGameWonPatch
{
	private static bool Prefix() =>
		PatchBridge.CharacterUnlock?.SuppressWinWrite() != true;
}
