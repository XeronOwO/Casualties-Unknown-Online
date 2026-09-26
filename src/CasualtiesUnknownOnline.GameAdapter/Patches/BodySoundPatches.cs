using System.Collections;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The local body's own one-shot sounds that are played from a COROUTINE — the
/// Vomiter vomit routines (<c>"vomit1"</c> / <c>"vomit2"</c>, Vomiter.cs:86/125),
/// the nap <c>"stretch"</c> (Body.cs:2510) and the water <c>"dogshake"</c>
/// (:2553). Each clip plays inside the coroutine body, so the capture scope is
/// entered per step (<see cref="ScopedCoroutine"/>); a Prefix/Postfix pair
/// around the method would be disposed before the first body statement.
/// <para>
/// Only the LOCAL PLAYER's body wraps: a render clone (a
/// <see cref="RemoteBodyDriver"/> in the parents) and every non-player body keep
/// the native coroutine untouched, so a clone can never report the owner's
/// sound as this player's action. The 2D <c>vomitwarning</c> /
/// <c>bloodvomitwarning</c> prompts play in <c>Vomiter.Vomit</c> /
/// <c>VomitBlood</c>, outside every wrapped routine, and stay the acting
/// player's own screen feedback by decision.
/// </para>
/// </summary>
internal static class BodySoundPatches
{
	[HarmonyPatch(typeof(Vomiter), "DoVomit")]
	internal static class VomitSoundPatch
	{
		private static void Postfix(Vomiter __instance, ref IEnumerator __result) =>
			__result = Wrap(__instance.GetComponent<Body>(), __result);
	}

	[HarmonyPatch(typeof(Vomiter), "DoBloodVomit")]
	internal static class BloodVomitSoundPatch
	{
		private static void Postfix(Vomiter __instance, ref IEnumerator __result) =>
			__result = Wrap(__instance.GetComponent<Body>(), __result);
	}

	[HarmonyPatch(typeof(Body), "NapCoroutine")]
	internal static class NapStretchSoundPatch
	{
		private static void Postfix(Body __instance, ref IEnumerator __result) =>
			__result = Wrap(__instance, __result);
	}

	[HarmonyPatch(typeof(Body), "WaterShake")]
	internal static class WaterShakeSoundPatch
	{
		private static void Postfix(Body __instance, ref IEnumerator __result) =>
			__result = Wrap(__instance, __result);
	}

	private static IEnumerator Wrap(Body? body, IEnumerator result) =>
		CaptureScopeGuard.IsLocalPlayerBody(body) && CaptureScopeGuard.IsLocalAction()
			? ScopedCoroutine.Capture(result, CallContext.Origin.CharacterBodySound)
			: result;
}
