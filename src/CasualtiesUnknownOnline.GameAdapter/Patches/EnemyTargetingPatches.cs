using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Host-side multiplayer enemy targeting. The game's enemy AI discovers players
/// through physics queries / PlayerCamera.main.body, which only see the LOCAL
/// body — remote render clones have all colliders disabled (RemoteBodyFactory,
/// by design). These thin patches let the EnemyCombatDirector resolve the
/// nearest in-world player instead:
///  - SpiderHandler.Update: after the game recomputes its move target (the
///    moveTime reset edge), the director overwrites it with the nearest player
///    inside seeDistance;
///  - CrystalEnemy.body getter: the director returns the nearest player body
///    inside the game's 64-unit close radius;
///  - CrystalEnemy.Lunge: the director orders the remote victim to apply the
///    lunge locally when the game's RaycastAll cannot see the collider-less
///    clone.
/// Guest-side frozen copies never reach these callbacks (EnemyPatches skips
/// Update/FixedUpdate; the director is host-only).
/// </summary>
internal static class EnemyTargetingPatches
{
	[HarmonyPatch(typeof(SpiderHandler), "Update")]
	internal static class SpiderHandlerTargetPatch
	{
		private static void Prefix(SpiderHandler __instance, out float __state) => __state = __instance.moveTime;

		private static void Postfix(SpiderHandler __instance, float __state)
		{
			// The game recomputed its target exactly when moveTime entered the
			// frame already expired (SpiderHandler.cs:95). A post-bite retreat
			// (moveTime = retreatMoveTime) stays untouched until it expires,
			// preserving the game's retreat semantics.
			if (__state <= 0f)
			{
				PatchBridge.Impl?.OnSpiderTargetDecided(__instance);
			}
		}
	}

	[HarmonyPatch(typeof(CrystalEnemy), "get_body")]
	internal static class CrystalEnemyBodyPatch
	{
		private static void Postfix(CrystalEnemy __instance, ref Body __result) =>
			PatchBridge.Impl?.OnCrystalEnemyBodyResolved(__instance, ref __result);
	}

	[HarmonyPatch(typeof(CrystalEnemy), "Lunge")]
	internal static class CrystalEnemyLungePatch
	{
		private static void Prefix(CrystalEnemy __instance) => PatchBridge.Impl?.OnCrystalLunge(__instance);
	}
}
