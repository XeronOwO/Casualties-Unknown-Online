using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// The ending lab's own <c>Update</c> (EndLabScript.cs:234-434) reads
/// <c>PlayerCamera.main.body</c> and <c>WorldGeneration.world</c> every frame. When the
/// host returns to the menu after the last-layer case, the scene is torn down while the
/// component can still receive one more frame; the native body then throws a
/// NullReferenceException from its very first field access. The world owns the queue at
/// that point, so there is nothing for the ending sequence to animate.
/// </summary>
internal static class EndLabScriptUpdatePatch
{
	[HarmonyPatch(typeof(EndLabScript), "Update")]
	internal static class EndLabScriptUpdatePrefix
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
