using System.Reflection;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The game's status icons present the WATCHED player while spectating, not the watcher's own body. The moodle
/// manager takes its body once in <c>Start</c> (<c>this.body = PlayerCamera.main.body;</c>) into a private field and
/// every later read — bleeding, pain, hunger, the condition icons themselves — comes from it, so pointing that field
/// at the followed clone for the duration of a watch is what makes the HUD look like the player the user is watching
/// is the one playing. The mode's own gates keep input and the local body's own presentation away, and the manager's
/// writes (<c>body.overdoseIndex--</c> in its update) land on the clone, never on the local body. The field is private,
/// which is why this patch goes through reflection once and caches the handle.
/// </summary>
[HarmonyPatch(typeof(MoodleManager), "Update")]
internal static class MoodleManagerSpectatorBodyPatch
{
	private static readonly FieldInfo? BodyField = AccessTools.Field(typeof(MoodleManager), "body");

	private static void Prefix(MoodleManager __instance)
	{
		if (!SpectatorPresentationGate.HasHudBody || BodyField is null)
		{
			return;
		}

		var watched = SpectatorPresentationGate.HudBody;
		if (watched == null) // Unity object — ==
		{
			return;
		}

		if (BodyField.GetValue(__instance) as Body != watched) // Unity object — ==
		{
			BodyField.SetValue(__instance, watched);
		}
	}
}
