using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// A BuildingEntity started — thin adapter to EntitySpawnSync: inside world
/// generation the entity is deterministic (nothing to do); outside it in a
/// session it is a runtime creation (the spawn command) and gets reported so
/// the peers create the same entity at the same place. The entity-event
/// channel's position-keyed identity then holds for it too.
/// </summary>
[HarmonyPatch(typeof(BuildingEntity), "Start")]
internal static class BuildingEntityStartPatch
{
	private static void Postfix(BuildingEntity __instance) => PatchBridge.Impl?.OnEntityInstantiated(__instance);
}
