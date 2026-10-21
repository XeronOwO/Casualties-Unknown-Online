using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The generation fades must not black out the loading screen the group is waiting on.
/// Two fades land inside the gate window: the finish-generation fade
/// (WorldGeneration.cs:3620) during the start-gate wait, and the layer descent's own
/// opening fade (WorldGeneration.cs:1045) on both roles once the descent edge opens the
/// window — the descent is a loading-screen transition now, not a black flash. Skipping
/// them keeps "still loading" continuous. Every other fade runs as shipped.
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
