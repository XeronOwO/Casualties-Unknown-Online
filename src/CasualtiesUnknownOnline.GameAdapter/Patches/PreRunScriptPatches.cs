using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Run-start gate: StartRun is the "begin a new run" entry. A guest whose
/// handshake is still pending has no host world params yet — starting anyway
/// would generate a random world that cannot match the host's. Block the start
/// and tell them to retry; once connected, world params arrive and the gate
/// opens (capture/apply itself happens at WorldGeneration.GenerateWorld, the
/// true generation boundary).
/// </summary>
[HarmonyPatch(typeof(PreRunScript), "StartRun")]
internal static class PreRunScriptStartRunPatch
{
	private static bool Prefix()
	{
		var adapter = GameAdapter.Instance;
		return adapter is null || adapter.OnStartRun();
	}
}
