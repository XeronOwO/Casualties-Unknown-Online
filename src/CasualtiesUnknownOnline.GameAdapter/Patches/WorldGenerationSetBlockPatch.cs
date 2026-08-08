using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Host-side damage-table capture: every post-generation SetBlock is a world
/// mutation (mining/destruction, remote damage application, earthquakes,
/// building) — record it so re-joining guests get the full accumulated state
/// (late-joiner full snapshot, architecture.md). Generation itself is the
/// baseline and is excluded via generatingWorld; SetBlockNoUpdate is
/// generation-only and intentionally not hooked.
/// </summary>
[HarmonyPatch(typeof(WorldGeneration), "SetBlock")]
internal static class WorldGenerationSetBlockPatch
{
	private static void Postfix(Vector2Int pos, ushort block)
	{
		var adapter = GameAdapter.Instance;
		if (adapter == null || !adapter.IsHostMode)
		{
			return;
		}

		adapter.OnBlockSet(pos, block);
	}
}
