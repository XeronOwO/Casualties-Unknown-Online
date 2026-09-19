using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// World-time authority (host owns Time.timeScale). In a live session every
/// SetTimeScale call is routed through the world-time domain:
/// - a CUO authoritative apply (WorldTimeApply) runs unchanged;
/// - the vanilla unconscious-screen fast-forward is suppressed by its own
///   HandleUnconsciousScreen scope before it reaches the bridge;
/// - the host's local calls run (authority) and the postfix reports the
///   applied speed for broadcast;
/// - a guest's Normal/Fast/SuperFast local call is LOCAL FIRST: it writes this
///   client's clock at once and becomes a WorldTimeRequest report that the host
///   answers; UnconsciousFast/DyingFast are host-owned and swallowed;
///   Slowmo/Paused and forced local transitions stay local-only (recorded
///   presentation semantics).
/// </summary>
[HarmonyPatch(typeof(PlayerCamera), "SetTimeScale")]
internal static class PlayerCameraSetTimeScalePatch
{
	private static bool Prefix(PlayerCamera.SpeedType speed, bool force)
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
			|| bridge.OnTimeScaleSetRequested(speed, force);
	}

	private static void Postfix(PlayerCamera.SpeedType speed) => PatchBridge.Impl?.OnLocalTimeScaleChanged(speed);
}
