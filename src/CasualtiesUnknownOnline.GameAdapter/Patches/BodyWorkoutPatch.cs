using CasualtiesUnknownOnline.GameAdapter.Character;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Captures the requested <c>Body.DoWorkout</c> type on the local body. The
/// decompiled method is an iterator (Body.cs:368-435): its body runs later in
/// the compiler-generated MoveNext, but the WorkoutType argument is known at
/// the original method's invocation, so a simple Prefix is enough to record
/// which exercise the owner started. The actual active/inactive decision still
/// comes from <c>Body.exercising</c>, which the original coroutine owns.
/// Render clones never call DoWorkout (their Body.Update is skipped), so a
/// remote proxy never receives this tracker.
/// </summary>
[HarmonyPatch(typeof(Body), "DoWorkout")]
internal static class BodyWorkoutPatch
{
	private static void Prefix(Body __instance, Body.WorkoutType type)
	{
		if (__instance.GetComponentInParent<RemoteBodyDriver>() != null
			|| __instance.GetComponent<CarriedBodyDriver>() != null) // Unity objects — ==
		{
			return;
		}

		var tracker = __instance.GetComponent<LocalWorkoutTracker>();
		if (tracker == null) // Unity object — ==
		{
			tracker = __instance.gameObject.AddComponent<LocalWorkoutTracker>();
		}

		tracker.WorkoutType = (byte)type;
	}
}
