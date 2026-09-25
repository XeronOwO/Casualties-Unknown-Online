using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// World-time authority (host owns Time.timeScale). In a live session every
/// SetTimeScale call is routed through the world-time domain, and the native
/// flags decide what the call is (WorldTimeScaleCall):
/// - a CUO authoritative apply (WorldTimeApply) runs unchanged;
/// - the vanilla unconscious-screen fast-forward is suppressed by its own
///   HandleUnconsciousScreen scope before it reaches the bridge;
/// - the host's local calls run (authority) and the postfix reports the
///   applied speed for broadcast;
/// - a guest's Normal/Fast/SuperFast local call is LOCAL FIRST: it writes this
///   client's clock at once and becomes a WorldTimeRequest report that the host
///   answers; UnconsciousFast/DyingFast are host-owned and swallowed;
///   Slowmo/Paused and forced local transitions stay local-only (recorded
///   presentation semantics);
/// - a silent automatic reset (switchSound false and not forced — above all the
///   native movement rule, PlayerCamera.cs:921-924) is NOT a speed intent: it is
///   swallowed on BOTH sides so the standing acceleration survives it and the
///   clock is not dipped (user ruling 2026-09-21, decision 223).
/// Both patch methods therefore take the native flags: a router that cannot see
/// switchSound cannot tell a speed change from a reset.
/// </summary>
[HarmonyPatch(typeof(PlayerCamera), "SetTimeScale")]
internal static class PlayerCameraSetTimeScalePatch
{
	private static bool Prefix(PlayerCamera.SpeedType speed, bool switchSound, bool force)
	{
		var origin = CallContext.Current;
		if (origin == CallContext.Origin.WorldTimeApply)
		{
			return true; // the world-time domain is applying the authoritative speed
		}

		if (origin == CallContext.Origin.WorldTimeSleepLocal)
		{
			return false; // the vanilla per-side sleep fast-forward never writes timeScale in a session
		}

		return PatchBridge.Impl is not { IsSessionActive: true } bridge
			|| bridge.OnTimeScaleSetRequested(speed, switchSound, force);
	}

	private static void Postfix(PlayerCamera.SpeedType speed, bool switchSound, bool force) =>
		PatchBridge.Impl?.OnLocalTimeScaleChanged(speed, switchSound, force);
}
