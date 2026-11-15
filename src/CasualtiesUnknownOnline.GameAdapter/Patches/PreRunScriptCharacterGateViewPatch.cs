using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The native portrait paint reads the solo unlock store every frame. After it
/// runs, the session view re-applies the button, tooltip and palette decision,
/// because <c>PreRunScript.Awake</c> disabled the locked buttons before a host
/// created its session (and a guest may join after the menu loaded). The native
/// paint stays untouched outside a session; the patch only replaces the colors
/// the session makes pickable.
/// </summary>
[HarmonyPatch(typeof(PreRunScript), "HandleCharacterSelect")]
internal static class PreRunScriptCharacterGateViewPatch
{
	private static void Postfix(PreRunScript __instance) =>
		PatchBridge.CharacterUnlock?.ApplyPicker(__instance);
}
