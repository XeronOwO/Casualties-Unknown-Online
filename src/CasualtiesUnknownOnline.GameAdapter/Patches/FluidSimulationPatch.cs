using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The fluid simulation is replaced in a session: the host simulates the world
/// grid ALONE (FluidSimulationAuthority — a multi-member pass over every
/// member's viewport, so the water flows around the guests too), the guests
/// render the streamed regions and never simulate (a per-side step would
/// consume the PUBLIC random stream differently on each side and drift
/// forever). Solo (no session): the original per-side step runs unchanged.
/// </summary>
[HarmonyPatch(typeof(FluidManager), "FixedUpdate")]
internal static class FluidSimulationPatch
{
	private static bool Prefix()
	{
		if (PatchBridge.Impl is not { } bridge || !bridge.IsSessionActive)
		{
			return true; // solo — the original per-side simulation
		}

		bridge.OnFluidFixedUpdate(); // host: the multi-member pass; guest: nothing
		return false;
	}
}
