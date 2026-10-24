using System.Reflection;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The intro cover: <c>PreRunScript.Start</c> shows the content warning unless the static <c>didIntro</c> flag is
/// set (the flag also gates the intro lore text in <c>TryLore</c>). What may set it is
/// <see cref="Runtime.GameAdapter.IntroCoverDecision"/> — the player's own local preference for an ordinary launch,
/// and never for a launch that came through Steam's join action (the user's 2026-10-24 ruling).
///
/// <para>
/// The decision's own sentence is written to the log here, once per <c>PreRunScript.Start</c> (a low-frequency
/// edge), so a reading of this ticket's launch cases tells which rule fired instead of inferring it from a screen
/// that is not there.
/// </para>
/// </summary>
[HarmonyPatch(typeof(PreRunScript), "Start")]
internal static class PreRunScriptIntroSkipPatch
{
	private static void Prefix()
	{
		var decision = GameAdapter.IntroCover;
		GameAdapter.LogIntroCover(decision.Describe());
		if (!decision.SkipsTheCover)
		{
			return;
		}

		typeof(PreRunScript).GetField("didIntro",
			BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, true);
	}
}
