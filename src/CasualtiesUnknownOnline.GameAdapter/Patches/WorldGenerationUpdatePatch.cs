using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Earthquake sync (host authority). The quake timer runs on EVERY side with
/// INDEPENDENT random (WorldGeneration.cs:865-869) — four sides would quake at
/// four times and break four different regions, stripping the terrain. The
/// host broadcasts the quake start (EarthquakeStart); guests show the effect
/// (earthquakeTime drives the Update intensity ramp) and their OWN timer is
/// FROZEN (the Prefix caps earthquakeDelay at float.MaxValue every frame, so
/// the Update trigger — delay < 0, WorldGeneration.cs:866 — never fires and a
/// guest quake never STARTS locally; the guest's earthquakeTime only ever
/// turns positive through the host's broadcast). Their Update-frame SetBlock(0)
/// writes (the quake breaks + environment breaks, WorldGeneration.cs:895/1275)
/// are suppressed — the host's air-write relay applies them (see
/// WorldGenerationSetBlockPatch).
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

		// Guest in a live session: FREEZE the local quake timer. The game rolls
		// delay < 0 → earthquakeTime = Random (WorldGeneration.cs:866-871), i.e.
		// the quake STARTS before any postfix could cancel it — canceling after
		// the start still ran the trigger frame (started then ended). Capping
		// the delay keeps the trigger from ever firing: the guest never starts
		// a quake, only the host's broadcast does (OnEarthquakeStartReceived).
		//
		// The radiation line is the same host-authority world state: a guest's
		// own layerTimeSpent must not Activate() its local line independently.
		// layerTimeSpent is otherwise consumed only by the line condition
		// (WorldGeneration.cs:859-863), so capping it is side-effect free.
		if (PatchBridge.Impl is { IsSessionActive: true } bridge && bridge.IsHostMode == false)
		{
			__instance.earthquakeDelay = float.MaxValue;
			__instance.layerTimeSpent = Mathf.Min(__instance.layerTimeSpent, __instance.maxTimePerLayer);
		}
	}

	private static void Postfix(WorldGeneration __instance)
	{
		InUpdate = false;

		// A quake just started on this side: earthquakeTime flipped from <= 0
		// to > 0. The HOST broadcasts it (timing sync + duration + its next
		// delay — the Update logic already rolled a fresh earthquakeDelay).
		// A guest start observed here means the freeze leaked or the host's
		// broadcast set the timer this frame (frame-order dependent) — the
		// world domain decides what to do (it never cancels: a broadcast-driven
		// quake must play).
		if (__instance.earthquakeTime > 0f && _prevEarthquakeTime <= 0f)
		{
			PatchBridge.Impl?.OnEarthquakeStarted(__instance.earthquakeTime, __instance.earthquakeDelay);
		}

	}
}
