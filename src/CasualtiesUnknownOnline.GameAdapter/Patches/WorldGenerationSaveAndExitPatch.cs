using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The end-of-layer panel's "Save and exit". Native `WorldGeneration.SaveAndExit` advances the depth, writes the
/// game's own `save.sv` through `SaveSystem.SaveGame()` and returns to the main menu; a CUO client must not produce
/// that file at all (the user's 2026-10-24 ruling: "游戏原生存档对我们 cuo 没有意义"), because CUO's saves are its
/// own world archives and the native one is written with the caller's own world state — which, on a member, is a
/// layer the session never agreed on.
///
/// <para>
/// So the entry is replaced on a client where CUO runs: the save half arms a CUO cut (`menu-return`) and the entry
/// leaves to the menu itself, while the native body is skipped entirely. When CUO cannot take a cut (no save
/// repository, no world) the port answers false and the native entry keeps the game's behaviour.
/// </para>
/// </summary>
[HarmonyPatch(typeof(WorldGeneration), "SaveAndExit")]
internal static class WorldGenerationSaveAndExitPatch
{
	private static bool Prefix() => PatchBridge.SaveCut?.TakeWorldCutInsteadOfTheNativeSave() != true;
}
