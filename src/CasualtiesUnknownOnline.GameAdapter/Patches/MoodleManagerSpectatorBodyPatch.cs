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
		if (BodyField is null)
		{
			return;
		}

		// This patch owns the field it repoints: when the watch ends the native manager must get the local
		// body back in the SAME frame, before UpdateMoodles runs. Returning early here left the private field
		// pointing at the followed clone; after the clone is destroyed, AddAllMoodles calls TryGetComponent on
		// it every frame (the 2026-11-15 host freeze: thousands of NullReferenceExceptions and the UI stops
		// responding).
		var camera = PlayerCamera.main;
		var desired = SpectatorPresentationGate.HasHudBody
			? SpectatorPresentationGate.HudBody
			: camera != null ? camera.body : null; // Unity objects — ==
		if (desired == null) // Unity object — ==
		{
			return;
		}

		if (BodyField.GetValue(__instance) as Body != desired) // Unity object — ==
		{
			BodyField.SetValue(__instance, desired);
		}
	}
}
