using CasualtiesUnknownOnline.GameAdapter.Character;
using HarmonyLib;

namespace CasualtiesUnknownOnline.GameAdapter.Patches;

/// <summary>
/// Captures which nap coroutine the local body started. <c>Body.TakeANap</c>
/// chooses <c>NapCoroutine</c> or <c>AltNapCoroutine</c> from
/// sickness/happiness/temperature (Body.cs:2484-2498), but neither exposes the
/// chosen variant as a field; these two iterator prefixes run when the
/// coroutine is started (the same call-identity trick as
/// <see cref="BodyWorkoutPatch"/> on Body.DoWorkout) and store the wire
/// variant on a tiny <see cref="LocalNapTracker"/>. Render clones never call
/// TakeANap, so they never receive this tracker.
/// </summary>
internal static class BodyNapPatch
{
	private static void Mark(Body body, byte napVariant)
	{
		if (body.GetComponentInParent<RemoteBodyDriver>() != null
			|| CarriedBodyDriver.IsCarrying(body)) // Unity objects — ==
		{
			return;
		}

		var tracker = body.GetComponent<LocalNapTracker>();
		if (tracker == null) // Unity object — ==
		{
			tracker = body.gameObject.AddComponent<LocalNapTracker>();
		}

		tracker.NapVariant = napVariant;
	}

	[HarmonyPatch(typeof(Body), "NapCoroutine")]
	internal static class NapCoroutinePatch
	{
		private static void Prefix(Body __instance) => Mark(__instance, NapPresentation.Normal);
	}

	[HarmonyPatch(typeof(Body), "AltNapCoroutine")]
	internal static class AltNapCoroutinePatch
	{
		private static void Prefix(Body __instance) => Mark(__instance, NapPresentation.Alt);
	}
}
