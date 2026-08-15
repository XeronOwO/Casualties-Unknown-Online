using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Enemy-proximity side-effect hooks. The game's own callbacks mutate the
/// LOCAL body only (ElderThornbackBehaviour.cs:43-101, XalorisScript.cs:23-31);
/// the patch verifies the game's own transition edge (the private timer field
/// changed) and reports the post-effect terminal state as the dedicated
/// EnemyEffectMsg — never the 1 Hz snapshot. The patches are thin adapters:
/// the EnemyProximitySync domain owns capture/report/apply.
/// </summary>
internal static class EnemyProximityPatches
{
	[HarmonyPatch(typeof(ElderThornbackBehaviour), "Update")]
	internal static class ElderThornbackUpdatePatch
	{
		private static void Prefix(ElderThornbackBehaviour __instance, out float __state) =>
			__state = Traverse.Create(__instance).Field("timeChecked").GetValue<float>();

		private static void Postfix(ElderThornbackBehaviour __instance, float __state)
		{
			var current = Traverse.Create(__instance).Field("timeChecked").GetValue<float>();
			if (current == __state)
			{
				return; // not the 1 s tick edge
			}

			var body = LocalBody();
			if (body == null) // Unity object — ==
			{
				return;
			}

			var distance = Vector2.Distance(__instance.transform.position, body.transform.position);
			if (distance < ElderThornbackBehaviour.maxDistance
				|| distance < ElderThornbackBehaviour.maxDistance * 2.25f)
			{
				PatchBridge.Impl?.OnElderHorrorTick(body);
			}
		}
	}

	[HarmonyPatch(typeof(ElderThornbackBehaviour), "OnDestroy")]
	internal static class ElderThornbackDestroyPatch
	{
		private static void Postfix(ElderThornbackBehaviour __instance)
		{
			var body = LocalBody();
			if (body == null) // Unity object — ==
			{
				return;
			}

			var build = Traverse.Create(__instance).Field("build").GetValue<BuildingEntity>();
			if (build == null) // Unity object — ==
			{
				return;
			}

			// Only the death reward branch (health <= 0 and within the field) —
			// a scene unload or a surviving elder must not report.
			if (build.health <= 0f
				&& Vector2.Distance(__instance.transform.position, body.transform.position) < ElderThornbackBehaviour.maxDistance)
			{
				PatchBridge.Impl?.OnElderHorrorDefeat(body);
			}
		}
	}

	[HarmonyPatch(typeof(XalorisScript), "OnWillRenderObject")]
	internal static class XalorisTickPatch
	{
		private static void Prefix(XalorisScript __instance, out float __state) =>
			__state = Traverse.Create(__instance).Field("lastTime").GetValue<float>();

		private static void Postfix(XalorisScript __instance, float __state)
		{
			var current = Traverse.Create(__instance).Field("lastTime").GetValue<float>();
			if (current == __state)
			{
				return; // not the 0.5 s tick edge
			}

			var body = LocalBody();
			if (body == null) // Unity object — ==
			{
				return;
			}

			if (Vector2.Distance(__instance.transform.position, body.transform.position) < 5.5f)
			{
				PatchBridge.Impl?.OnXalorisSepticTick(body);
			}
		}
	}

	private static Body? LocalBody()
	{
		var playerCamera = PlayerCamera.main;
		return playerCamera != null ? playerCamera.body : null; // Unity objects — ==
	}
}
