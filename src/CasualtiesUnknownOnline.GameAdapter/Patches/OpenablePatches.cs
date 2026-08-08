using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Lockable-entity open hooks: instant-open (Openable.cs:12), lockpick
/// (LockpingMinigame.cs:129) and keypad (KeypadMinigame.cs:138) success all
/// write health = 0 DIRECTLY — none of them goes through attack damage, so
/// the entity-damage hook misses them and the peer's crate would never open
/// (its drops are rolled by the host's own copy). Each hook snapshots the
/// target's health and reports the open on the frame it drops to zero (the
/// write itself is not patchable — it is a plain field write).
/// </summary>
internal static class OpenablePatches
{
	[HarmonyPatch(typeof(Openable), "OnUse")]
	internal static class OpenableOnUsePatch
	{
		private static float _healthBefore = -1f;

		private static void Prefix(Openable __instance)
		{
			var build = __instance.GetComponent<BuildingEntity>();
			_healthBefore = build != null ? build.health : -1f; // Unity object — ==
		}

		private static void Postfix(Openable __instance)
		{
			var build = __instance.GetComponent<BuildingEntity>();
			if (build != null && _healthBefore > 0f && build.health == 0f) // Unity object — ==; instant-open path
			{
				PatchBridge.Impl?.OnBuildingEntityOpened(build);
			}
		}
	}

	[HarmonyPatch(typeof(LockpingMinigame), "Update")]
	internal static class LockpingMinigamePatch
	{
		private static float _healthBefore = -1f;

		private static void Prefix(LockpingMinigame __instance) =>
			_healthBefore = __instance.toDestroy != null ? __instance.toDestroy.health : -1f; // Unity object — ==

		private static void Postfix(LockpingMinigame __instance)
		{
			if (__instance.toDestroy != null && _healthBefore > 0f && __instance.toDestroy.health == 0f) // Unity object — ==
			{
				PatchBridge.Impl?.OnBuildingEntityOpened(__instance.toDestroy);
			}
		}
	}

	[HarmonyPatch(typeof(KeypadMinigame), "Update")]
	internal static class KeypadMinigamePatch
	{
		private static float _healthBefore = -1f;

		private static void Prefix(KeypadMinigame __instance) =>
			_healthBefore = __instance.toDestroy != null ? __instance.toDestroy.health : -1f; // Unity object — ==

		private static void Postfix(KeypadMinigame __instance)
		{
			if (__instance.toDestroy != null && _healthBefore > 0f && __instance.toDestroy.health == 0f) // Unity object — ==
			{
				PatchBridge.Impl?.OnBuildingEntityOpened(__instance.toDestroy);
			}
		}
	}
}
