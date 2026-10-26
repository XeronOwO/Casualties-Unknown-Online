using CasualtiesUnknownOnline.GameAdapter.Run;
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
/// <para>
/// It is also where the HOST's configured run-settings preset is applied, and it has to be here rather than later: the preset changes the values
/// <see cref="PreRunScript"/> hands to world generation, so applying it after <c>StartRun</c> would be too late to matter. The game's own
/// <c>ApplyPreset</c> (<c>PreRunScript.cs:126-140</c>) is what runs — CUO invents no difficulty of its own.
/// </para>
/// </summary>
[HarmonyPatch(typeof(PreRunScript), "StartRun")]
internal static class PreRunScriptStartRunPatch
{
	private static bool Prefix(PreRunScript __instance)
	{
		if (PatchBridge.Impl is null)
		{
			return true;
		}

		if (!PatchBridge.Impl.OnGuestStartAttempt())
		{
			// A guest's StartRun is refused, so no preset is applied on top of a start that does not happen.
			return false;
		}

		// The HOST's world, or a standalone start with no session: a MEMBER follows the host's run and must generate the same settings it did, so its
		// own config must not overwrite them (the member path is the one that calls this method to follow a WorldJoin).
		if ((PatchBridge.Impl.IsHostMode || !PatchBridge.Impl.IsSessionActive) && RunPresetDecision.Preset is { Length: > 0 } preset)
		{
			// The game's own loader; an unknown name throws from `RunSettings.GetPreset`, which is the loud failure a typo deserves.
			__instance.ApplyPreset(preset);
		}

		PatchBridge.Impl.OnWorldJoinRequested(isTutorial: false);
		return true;
	}
}
