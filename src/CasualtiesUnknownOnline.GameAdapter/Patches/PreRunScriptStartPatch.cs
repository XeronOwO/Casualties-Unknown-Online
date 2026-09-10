using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The Continue button's interactable state follows the CUO world repository,
/// not the native save (decision 165; acceptance: the entry is reachable on a
/// machine that has CUO worlds but no <c>save.sv</c>, and is not reachable on a
/// machine whose only save is the native one CUO refuses to read).
///
/// <see cref="PreRunScript.Start"/> sets
/// <c>loadButton.interactable = SaveSystem.HasSave()</c> and then reads
/// <c>save.sv</c> for the icon and the played-time label; CUO overrides only
/// the reachability, reusing the game's own entry — no parallel menu surface.
/// A lobby-bound guest's button is re-disabled every frame by
/// <see cref="Run.GuestMenuGuard"/>, which still owns that case.
/// </summary>
[HarmonyPatch(typeof(PreRunScript), "Start")]
internal static class PreRunScriptStartPatch
{
	private static void Postfix(PreRunScript __instance)
	{
		var bridge = PatchBridge.Impl;
		if (bridge is null || __instance?.loadButton == null) // Unity object — ==
		{
			return;
		}

		__instance.loadButton.interactable = bridge.HasRestorableWorld();
	}
}
