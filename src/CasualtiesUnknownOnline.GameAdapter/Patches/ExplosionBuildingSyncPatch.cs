using System.Collections.Generic;
using CasualtiesUnknownOnline.GameAdapter.World;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The building-damage diff around WorldGeneration.CreateExplosion — thin
/// adapter to ExplosionBuildingSync (the diff logic lives in the world
/// domain). Every explosion on either side (mine timeline, chain mines, the
/// host's applier, a player's own explosive) reports its structural damage
/// through the existing BuildingEntityDamaged channel.
/// </summary>
[HarmonyPatch(typeof(WorldGeneration), "CreateExplosion")]
internal static class ExplosionBuildingSyncPatch
{
	private static void Prefix(out List<(BuildingEntity, float)>? __state) => __state = ExplosionBuildingSync.Capture();

	private static void Postfix(List<(BuildingEntity, float)>? __state) => ExplosionBuildingSync.ReportDamaged(__state);
}
