using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Supplies the vanilla <c>WorldGeneration.GetBlockInfo</c> switch with
/// mod-authored <see cref="BlockInfo"/> entries for custom tile indices. The
/// prefix returns true (let the original switch run) for every vanilla block;
/// custom indices get the provider-built BlockInfo and the original is skipped.
/// </summary>
[HarmonyPatch(typeof(WorldGeneration), nameof(WorldGeneration.GetBlockInfo))]
internal static class WorldGenerationGetBlockInfoPatch
{
	private static bool Prefix(ushort block, ref BlockInfo __result)
	{
		var custom = PatchBridge.Impl?.TryGetCustomBlockInfo(block);
		if (custom is null)
		{
			return true;
		}

		__result = custom;
		return false;
	}
}
