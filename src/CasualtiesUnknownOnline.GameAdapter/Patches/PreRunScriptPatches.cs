using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Run-start gates: StartRun/LoadRun/StartTutorial are the three ways to enter
/// a world. In a session only the host may do so — a guest's world must follow
/// the host's (WorldJoin is the only entry); starting on its own would create
/// a world the host does not know.
/// </summary>
[HarmonyPatch(typeof(PreRunScript), "StartRun")]
internal static class PreRunScriptStartRunPatch
{
	private static bool Prefix() => PatchBridge.Impl is null || PatchBridge.Impl.OnGuestStartAttempt();
}

[HarmonyPatch(typeof(PreRunScript), "LoadRun")]
internal static class PreRunScriptLoadRunPatch
{
	private static bool Prefix() => PatchBridge.Impl is null || PatchBridge.Impl.OnGuestStartAttempt();
}

[HarmonyPatch(typeof(PreRunScript), "StartTutorial")]
internal static class PreRunScriptStartTutorialPatch
{
	private static bool Prefix() => PatchBridge.Impl is null || PatchBridge.Impl.OnGuestStartAttempt();
}
