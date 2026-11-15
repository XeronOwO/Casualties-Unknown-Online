using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The turret's own <c>Update</c> (TurretScript.cs:24-80) reads
/// <c>PlayerCamera.main.body</c> every frame to look for a target. A world leave tears the
/// camera's body down while a turret can still receive one more frame, and the native body
/// then throws a NullReferenceException from the range check. The turret has no target to
/// look for once the world is gone, so skip that frame.
/// </summary>
internal static class TurretScriptUpdatePatch
{
	[HarmonyPatch(typeof(TurretScript), "Update")]
	internal static class TurretScriptUpdatePrefix
	{
		private static bool Prefix()
		{
			// Unity objects use ==, not ReferenceEquals: a destroyed object compares equal to null.
			return WorldGeneration.world != null
				&& PlayerCamera.main != null
				&& PlayerCamera.main.body != null;
		}
	}
}
