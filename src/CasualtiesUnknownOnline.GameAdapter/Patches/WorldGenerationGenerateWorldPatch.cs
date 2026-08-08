using System.Collections;
using CasualtiesUnknownOnline.GameAdapter.WorldGen;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// World-generation boundary hook. GenerateWorld is the coroutine that starts
/// procedural generation — the only correct point to capture/apply the host's
/// Random.state: anything the game consumes from Random before this moment
/// (scene loading, menu/update-time randomness) is already baked into the
/// captured state, so host and guest continue from identical RNG streams.
///
/// In a live session the returned coroutine is wrapped (never replaced) by
/// WorldGenRandomIsolation: the game's own coroutine body — loading UI,
/// generatingWorld flag, step order — stays exactly as shipped, only its
/// random stream is sealed across every yield. Single-player (no session)
/// keeps the original coroutine untouched.
/// </summary>
[HarmonyPatch(typeof(WorldGeneration), "GenerateWorld")]
internal static class WorldGenerationGenerateWorldPatch
{
	private static void Prefix() => PatchBridge.Impl?.OnWorldGenerate(); // host: capture + publish params; guest: apply the host's

	private static void Postfix(ref IEnumerator __result)
	{
		var adapter = PatchBridge.Impl;
		if (adapter is { IsWorldGenIsolated: true } && __result is not null)
		{
			__result = WorldGenRandomIsolation.Wrap(__result, adapter);
		}
	}
}
