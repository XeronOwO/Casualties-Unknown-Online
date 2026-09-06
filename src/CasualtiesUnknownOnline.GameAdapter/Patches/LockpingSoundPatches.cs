using System;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Lockpick-failure pain capture. <c>LockpingMinigame.Update</c> plays
/// <c>"gore2"</c> when the lockpick is stuck (LockpingMinigame.cs:144-156),
/// raises the acting player's limb pain and damages claw health during a
/// hand-lockpick failure. That call is outside the <c>PantSound</c> one-shot
/// scopes, so without this scope the remote players never hear the local
/// player's lockpick-failure pain sound.
/// The scope is opened around the whole minigame update; the string
/// <c>"unlock"</c> success sound also runs inside the same method but is
/// deliberately not classified by <see cref="CharacterSoundPolicy"/> for this
/// origin, so only the failure pain is reported.
/// </summary>
internal static class LockpingSoundPatches
{
	[HarmonyPatch(typeof(LockpingMinigame), "Update")]
	internal static class LockpingMinigamePainPatch
	{
		private static void Prefix(LockpingMinigame __instance, out IDisposable? __state) =>
			__state = CallContext.Enter(CallContext.Origin.CharacterLockpickPain);

		private static void Postfix(IDisposable? __state) => __state?.Dispose();
	}
}
