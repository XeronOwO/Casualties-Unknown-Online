using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// 1.0.2's <c>Body.OnCollisionEnter2D</c> asks for the step sound
/// <c>"Cocrete"</c> (Body.cs:1257), while the footstep dictionary is keyed by the
/// block's <c>stepsound</c> names — <c>"Concrete"</c>. The miss throws
/// <see cref="System.Collections.Generic.KeyNotFoundException"/> from
/// <c>WorldGeneration.RandomStepSound</c> on the frame the body lands. Normalize the
/// known misspelling before the native dictionary lookup; every other name is left
/// untouched.
/// </summary>
internal static class WorldGenerationRandomStepSoundPatch
{
	[HarmonyPatch(typeof(WorldGeneration), nameof(WorldGeneration.RandomStepSound))]
	internal static class RandomStepSoundPrefix
	{
		private static void Prefix(ref string step)
		{
			if (step == "Cocrete")
			{
				step = "Concrete";
			}
		}
	}
}
