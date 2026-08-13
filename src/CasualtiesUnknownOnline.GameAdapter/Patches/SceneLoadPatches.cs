using HarmonyLib;
using UnityEngine.SceneManagement;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Scene-switch hook: every scene load in the game goes through
/// SceneManager.LoadScene(string) — PlayerCamera.ToMainMenu → "PreGen",
/// PreRunScript.StartRun → "SampleScene", WorldGeneration.ReloadScene → the
/// active scene's name (verified call sites, reversing/). The OLD scene's
/// objects are destroyed DURING the load — a world item's OnDestroy then
/// looks like a player-operation destroy while the session still reads as
/// alive, and the echo deletes the host's world copies (#191: 70/637 destroy
/// reports when a guest quit). The teardown suppression must therefore engage
/// BEFORE the load starts; the world-entry edge disengages it.
/// </summary>
[HarmonyPatch(typeof(SceneManager), nameof(SceneManager.LoadScene), [typeof(string)])]
internal static class SceneLoadPatches
{
	private static void Prefix() => PatchBridge.Impl?.OnSceneLoadBegin();
}
