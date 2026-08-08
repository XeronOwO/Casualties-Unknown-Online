using System.Reflection;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Join-Game launch: skip the content-warning/intro screen. PreRunScript.Start
/// shows the warning unless the static didIntro flag is set (it also gates the
/// intro lore text in TryLore) — setting it up front makes the menu usable
/// immediately, so the follow-host pump can start the run without the player
/// clicking through the intro first.
/// </summary>
[HarmonyPatch(typeof(PreRunScript), "Start")]
internal static class PreRunScriptIntroSkipPatch
{
	private static void Prefix()
	{
		if (GameAdapter.SkipIntro)
		{
			typeof(PreRunScript).GetField("didIntro",
				BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, true);
		}
	}
}
