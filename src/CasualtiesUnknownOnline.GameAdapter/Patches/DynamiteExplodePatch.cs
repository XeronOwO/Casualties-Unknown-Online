using CasualtiesUnknownOnline.GameAdapter.Items;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The player dynamite detonation hook (CustomItemBehaviour.DynamiteExplode,
/// CustomItemBehaviour.cs:563-572): the native explosion has already run
/// (destroy + CreateExplosion) — the postfix only reports the one-shot fact
/// (item id + position) so the host applies it to its own world and the peers
/// replay the body/visual segment. RemoteApply replays never call this method
/// (they use TrapVisualReplay.ReplayExplosion instead), so no re-report guard
/// is needed inside the patch.
/// </summary>
internal static class DynamiteExplodePatch
{
	[HarmonyPatch(typeof(CustomItemBehaviour), "DynamiteExplode")]
	internal static class DynamiteExplode
	{
		private static void Postfix(CustomItemBehaviour __instance)
		{
			if (__instance == null) // Unity object — ==
			{
				return;
			}

			var item = __instance.GetComponent<Item>();
			var idComp = item != null ? item.GetComponent<ItemInstanceId>() : null; // Unity object — ==
			var itemId = idComp != null ? idComp.Id : 0; // Unity object — ==
			PatchBridge.Impl?.OnDynamiteExploded(itemId, __instance.transform.position);
		}
	}
}
