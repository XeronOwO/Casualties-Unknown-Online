using System.Collections;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// World-generation boundary hook. GenerateWorld is the coroutine that starts
/// procedural generation — the only correct point to capture/apply the host's
/// Random.state: anything the game consumes from Random before this moment
/// (scene loading, menu/update-time randomness) is already baked into the
/// captured state, so host and guest continue from identical RNG streams.
///
/// In a live session the original coroutine is replaced with the
/// random-stream-isolated version (WorldGenRandomIsolation) — the captured
/// state stays identical on both sides across every cross-frame yield instead
/// of being polluted by the public stream. Single-player (no session) keeps
/// the original coroutine untouched.
/// </summary>
[HarmonyPatch(typeof(WorldGeneration), "GenerateWorld")]
internal static class WorldGenerationGenerateWorldPatch
{
	private static bool Prefix(WorldGeneration __instance, ref IEnumerator __result)
	{
		var adapter = GameAdapter.Instance;
		if (adapter == null)
		{
			return true;
		}

		adapter.OnWorldGenerate(); // host: capture + publish params; guest: apply the host's
		if (adapter.IsWorldGenIsolated)
		{
			__result = WorldGenRandomIsolation.CreateIsolatedGenerateWorld(__instance);
			return false;
		}

		return true;
	}
}
