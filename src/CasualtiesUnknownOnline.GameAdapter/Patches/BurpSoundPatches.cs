using System;
using CasualtiesUnknownOnline.GameAdapter.Character;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Meal-end burp capture. <c>Body.HandleVisuals</c> counts the burp timer down
/// and plays <c>"burp"</c> when it crosses zero (Body.cs:3137-3142) — 5-10 s
/// after the meal that armed it (<c>Body.Burp</c> / <c>Body.Eat</c>,
/// Body.cs:2253-2284), so it cannot ride the item-use scope opened around the
/// eating action: that call has long returned. Without this scope the other
/// players hear the meal but never its closing burp.
/// The scope wraps the whole per-frame method, the way
/// <see cref="LockpingSoundPatches"/> wraps the whole lockpick update; the
/// policy classifies exactly the <c>"burp"</c> clip for this origin, so any
/// other body sound a future game build adds here stays silent on the peers
/// instead of being invented by them.
/// Only the local player's own body reports, and that guard is load-bearing
/// rather than belt-and-braces: <c>BodyUpdatePatch</c> skips a render clone's
/// <c>Body.Update</c> but invokes <c>HandleVisuals</c> reflectively for every
/// clone each frame, so a clone DOES run this method — without the guard the
/// scope would open inside the clone's own presentation pass.
/// </summary>
internal static class BurpSoundPatches
{
	[HarmonyPatch(typeof(Body), "HandleVisuals")]
	internal static class BodyHandleVisualsBurpPatch
	{
		private static void Prefix(Body __instance, out IDisposable? __state) =>
			__state = __instance.GetComponentInParent<RemoteBodyDriver>() == null // Unity object — ==
				? CallContext.Enter(CallContext.Origin.CharacterBurp)
				: null;

		private static void Postfix(IDisposable? __state) => __state?.Dispose();
	}
}
