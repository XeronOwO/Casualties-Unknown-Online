using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Run-start gates: StartRun/LoadRun/StartTutorial are the three ways to enter
/// a world. In a session only the host may do so — a guest's world must follow
/// the host's (WorldJoin is the only entry); starting on its own would create
/// a world the host does not know. On the HOST, the entry ALSO fires the
/// WorldJoin instruction immediately — the guest starts its transition
/// animation and loading together with the host (not one animation late).
///
/// LoadRun is the native Continue entry, which decision 165 turns into the CUO
/// continue path: the host's click resolves to "restore the selected CUO world"
/// before the scene loads. When CUO cannot open a world the prefix BLOCKS the
/// original — the native path would read <c>save.sv</c> and silently regenerate
/// the layer, which the restore contract forbids.
/// </summary>
[HarmonyPatch(typeof(PreRunScript), "LoadRun")]
internal static class PreRunScriptLoadRunPatch
{
	private static bool Prefix()
	{
		var bridge = PatchBridge.Impl;
		if (bridge is null)
		{
			return true;
		}

		// The guest gate first: a guest may only enter on the host's instruction,
		// so its LoadRun never reaches the CUO restore.
		return bridge.OnGuestStartAttempt() && bridge.OnHostContinueRequested();
	}
}
