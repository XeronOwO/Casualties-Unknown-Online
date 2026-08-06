using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Run-start hook: StartRun is the "begin a new run" entry (sets run settings,
/// then loads SampleScene). The host captures world params here; the guest
/// applies the host's params so both sides generate the same world.
/// </summary>
[HarmonyPatch(typeof(PreRunScript), "StartRun")]
internal static class PreRunScriptStartRunPatch
{
	private static void Postfix()
	{
		GameAdapter.Instance?.OnStartRun();
	}
}
