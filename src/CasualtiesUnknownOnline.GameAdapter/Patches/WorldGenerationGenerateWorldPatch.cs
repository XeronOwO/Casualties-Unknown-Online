using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// World-generation boundary hook. GenerateWorld is the coroutine that starts
/// procedural generation — the only correct point to capture/apply the host's
/// Random.state: anything the game consumes from Random before this moment
/// (scene loading, menu/update-time randomness) is already baked into the
/// captured state, so host and guest continue from identical RNG streams
/// (KrokMP does the same, LastBeforeGenerationState).
/// </summary>
[HarmonyPatch(typeof(WorldGeneration), "GenerateWorld")]
internal static class WorldGenerationGenerateWorldPatch
{
	private static void Prefix() => GameAdapter.Instance?.OnWorldGenerate();
}
