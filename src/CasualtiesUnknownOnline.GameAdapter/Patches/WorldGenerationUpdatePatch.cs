using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Earthquake sync (host authority). The quake timer runs on EVERY side with
/// INDEPENDENT random (WorldGeneration.cs:865-869) — four sides would quake at
/// four times and break four different regions, stripping the terrain. The
/// host broadcasts the quake start (EarthquakeStart); guests show the effect
/// (earthquakeTime drives the Update intensity ramp) and lock their own timer;
/// their Update-frame SetBlock(0) writes (the quake breaks + environment
/// breaks, WorldGeneration.cs:895/1275) are suppressed — the host's air-write
/// relay applies them (see WorldGenerationSetBlockPatch).
/// </summary>
[HarmonyPatch(typeof(WorldGeneration), "Update")]
internal static class WorldGenerationUpdatePatch
{
	/// <summary>True while a WorldGeneration.Update frame runs — SetBlock calls inside are quake/environment writes.</summary>
	internal static bool InUpdate;

	private static float _prevEarthquakeTime;

	private static void Prefix(WorldGeneration __instance)
	{
		InUpdate = true;
		_prevEarthquakeTime = __instance.earthquakeTime;
	}

	private static void Postfix(WorldGeneration __instance)
	{
		InUpdate = false;

		// A quake just started on this side: earthquakeTime flipped from <= 0
		// to > 0. The HOST broadcasts it (timing sync + duration + its next
		// delay — the Update logic already rolled a fresh earthquakeDelay).
		if (__instance.earthquakeTime > 0f && _prevEarthquakeTime <= 0f)
		{
			PatchBridge.Impl?.OnEarthquakeStarted(__instance.earthquakeTime, __instance.earthquakeDelay);
		}
	}
}
