using CasualtiesUnknownOnline.GameAdapter.Content;
using CasualtiesUnknownOnline.GameAdapter.ModStatus;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Mod-status vanilla moodle-row hooks (phase 3 local UI seam). The moodle
/// manager clears and rebuilds its rows every ~0.5 s; important (main-row)
/// mod moodles are added in the <c>AddAllMoodles</c> prefix before the native
/// method flips <c>sideMoodles</c>, so they appear in the main row. The
/// postfix adds non-important mod moodles after the native side moodles.
/// </summary>
internal static class ModStatusMoodlePatches
{
	[HarmonyPatch(typeof(MoodleManager), "AddAllMoodles")]
	internal static class ModMoodlePatch
	{
		private static void Prefix(MoodleManager __instance) =>
			PatchBridge.Impl?.ApplyModMoodles(__instance, importantRow: true);

		private static void Postfix(MoodleManager __instance) =>
			PatchBridge.Impl?.ApplyModMoodles(__instance, importantRow: false);
	}

	[HarmonyPatch(typeof(Moodle), "Start")]
	internal static class MoodleAnimationPatch
	{
		private static void Postfix(Moodle __instance)
		{
			if (__instance == null || string.IsNullOrWhiteSpace(__instance.type)) // Unity object — ==
			{
				return;
			}

			if (!TryGetAnimation(__instance.type, out var frames, out var framesPerSecond, out var loop))
			{
				return;
			}

			if (__instance.transform.childCount == 0)
			{
				return;
			}

			var image = __instance.transform.GetChild(0).GetComponent<Image>();
			if (image == null) // Unity object — ==
			{
				return;
			}

			var animator = image.GetComponent<CustomImageAnimator>();
			if (animator == null) // Unity object — ==
			{
				animator = image.gameObject.AddComponent<CustomImageAnimator>();
			}

			animator.SetAnimation(frames, framesPerSecond, loop);
		}

		private static bool TryGetAnimation(
			string type,
			out Sprite[] frames,
			out float framesPerSecond,
			out bool loop)
		{
			if (MoodleAnimationRegistry.TryGet(type, out frames, out framesPerSecond, out loop))
			{
				return true;
			}

			var stripped = StripTrailingDigits(type);
			if (!string.Equals(stripped, type, System.StringComparison.Ordinal)
				&& MoodleAnimationRegistry.TryGet(stripped, out frames, out framesPerSecond, out loop))
			{
				return true;
			}

			frames = [];
			framesPerSecond = 0f;
			loop = true;
			return false;
		}

		private static string StripTrailingDigits(string value)
		{
			var end = value.Length;
			while (end > 0 && char.IsDigit(value[end - 1]))
			{
				end--;
			}

			return value.Substring(0, end);
		}
	}
}
