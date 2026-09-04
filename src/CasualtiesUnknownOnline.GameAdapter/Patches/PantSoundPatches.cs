using System;
using CasualtiesUnknownOnline.GameAdapter.Character;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// PantSound vocalization capture. <c>PantSound</c> is a Body-auto component
/// that still runs on a local player while the render-proxy patches skip
/// <c>Body.Update</c>/<c>FixedUpdate</c>; remote clones have it explicitly
/// disabled (<see cref="RemoteBodyFactory"/>), so these scopes are guarded to
/// local bodies only.
/// The scopes distinguish the one-shot vocalizations from the continuous
/// pant loop: the loop uses an <c>AudioSource</c>, not <c>Sound.Play</c>, so
/// it is never captured by the Sound.Play patches.
/// </summary>
internal static class PantSoundPatches
{
	private static bool IsLocalVocalizer(PantSound sound)
	{
		var body = sound.GetComponent<Body>();
		return body != null // Unity object — ==
			&& body.GetComponentInParent<RemoteBodyDriver>() == null;
	}

	[HarmonyPatch(typeof(PantSound), "Update")]
	internal static class PantSoundUpdatePatch
	{
		private static void Prefix(PantSound __instance, out IDisposable? __state) =>
			__state = IsLocalVocalizer(__instance)
				? CallContext.Enter(CallContext.Origin.CharacterVocalization)
				: null;

		private static void Postfix(IDisposable? __state) => __state?.Dispose();
	}

	[HarmonyPatch(typeof(PantSound), "Bark")]
	internal static class PantSoundBarkPatch
	{
		private static void Prefix(PantSound __instance, out IDisposable? __state) =>
			__state = IsLocalVocalizer(__instance)
				? CallContext.Enter(CallContext.Origin.CharacterBark)
				: null;

		private static void Postfix(IDisposable? __state) => __state?.Dispose();
	}

	[HarmonyPatch(typeof(PantSound), "TryGrowl")]
	internal static class PantSoundTryGrowlPatch
	{
		private static void Prefix(PantSound __instance, out IDisposable? __state) =>
			__state = IsLocalVocalizer(__instance)
				? CallContext.Enter(CallContext.Origin.CharacterGrowl)
				: null;

		private static void Postfix(IDisposable? __state) => __state?.Dispose();
	}
}
