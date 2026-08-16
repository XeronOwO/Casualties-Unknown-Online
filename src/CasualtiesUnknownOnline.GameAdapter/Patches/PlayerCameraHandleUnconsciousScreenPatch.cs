using System;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Sleep-acceleration suppression scope. HandleUnconsciousScreen writes
/// UnconsciousFast (25×) / DyingFast (3.5×) / Normal locally whenever the
/// local black screen is up (PlayerCamera.cs:2235-2244) — in multiplayer a
/// single sleeping player would fast-forward the whole shared world. The
/// scope makes every SetTimeScale call inside the method a no-op; the host's
/// WorldTimeSync applies the session speed only when EVERY in-world player is
/// unconscious. The scope is CallContext (not a static flag), so nested/
/// exception paths restore correctly.
/// </summary>
[HarmonyPatch(typeof(PlayerCamera), "HandleUnconsciousScreen")]
internal static class PlayerCameraHandleUnconsciousScreenPatch
{
	private static void Prefix(out IDisposable? __state) =>
		__state = CallContext.Enter(CallContext.Origin.WorldTimeSleepLocal);

	private static void Postfix(IDisposable? __state) => __state?.Dispose();
}
