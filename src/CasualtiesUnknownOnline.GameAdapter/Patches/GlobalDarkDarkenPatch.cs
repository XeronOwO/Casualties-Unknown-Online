using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The finish-generation fade (WorldGeneration.cs:3620) would black out the
/// guest's kept loading screen during the start-gate wait — it reads as
/// "black, then the wait" (the fade runs ~1.3 s: in, auto-out; the gate wait
/// and the kept loading screen are both underneath it). Skip the fade
/// entirely while the gate window holds (generation done or waiting for the
/// host's release) — the loading screen stays visible, the wait reads as
/// "still loading". Layer switches (RegenerateWorld.cs:1045, outside the gate
/// window) keep their own fade.
/// </summary>
[HarmonyPatch(typeof(GlobalDark), "Darken")]
internal static class GlobalDarkDarkenPatch
{
	private static bool Prefix()
	{
		if (PatchBridge.Impl is { } bridge && bridge.IsInGateWindow)
		{
			bridge.OnDarkenSkipped();
			return false;
		}

		return true;
	}
}
