using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Item-vs-enemy hit reporting: <see cref="SpiderHandler.OnCollisionEnter2D"/>
/// completed on the HOST side (the guest freeze prefix already skips the
/// original on frozen copies). The game's native item branch only runs within
/// 50 units of the local body; the bridge generalizes that proximity to the
/// in-world player set and relays the health damage through the existing
/// BuildingEntityDamaged event so every guest's frozen copy keeps the same
/// health/death/drop semantics as any other player-vs-entity hit. This is a
/// thin postfix — all decision/application state lives in EnemyCombatDirector.
/// </summary>
[HarmonyPatch(typeof(SpiderHandler), "OnCollisionEnter2D")]
internal static class EnemyItemHitPatch
{
	private static void Postfix(SpiderHandler __instance, Collision2D collision) =>
		PatchBridge.Impl?.OnEnemyItemCollision(__instance, collision);
}
