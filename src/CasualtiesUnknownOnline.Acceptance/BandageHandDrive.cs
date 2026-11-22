using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.Acceptance;

/// <summary>
/// Drives the bandage wrap's hand the way the platform pointer drives it: the hand is placed on the ring one
/// step at a time, and every other condition of the wrap — the click-start window, the twenty-degree ratchet,
/// the hand coming back under ninety degrees to finish — stays the game's own.
///
/// Why this exists (2026-11-23): the game reads the platform pointer for the hand, so a staged pointer never
/// moves it; the acceptance suite cannot move the real cursor (the tools are forbidden OS input), and a hand
/// written once per recipe call is overwritten by the next frame's physics. A per-frame placement is the only
/// path that leaves the wrap's own conditions in the game's hands.
/// </summary>
public static class BandageHandDrive
{
	private static Harmony? _harmony;

	internal static bool Active { get; private set; }

	internal static float Radius { get; private set; }

	internal static float Angle { get; private set; }

	internal static float Step { get; private set; }

	internal static int Frames { get; private set; }

	internal static void Install(Harmony harmony)
	{
		if (_harmony != null)
		{
			return;
		}

		var target = AccessTools.Method("BandageMinigame:PhysicsUpdate");
		if (target == null)
		{
			return;
		}

		harmony.Patch(target, prefix: new HarmonyMethod(typeof(BandageHandDrive), nameof(BeforePhysicsUpdate)));
		_harmony = harmony;
	}

	public static void Start(float radius, float step)
	{
		Radius = radius > 1f ? radius : 250f;
		Step = step > 0f ? step : 20f;
		Angle = 0f;
		Frames = 0;
		Active = true;
	}

	public static void Stop() => Active = false;

	/// <summary>Places the hand on the ring before the wrap reads it, and walks one step a frame.</summary>
	private static void BeforePhysicsUpdate(object __instance)
	{
		if (!Active)
		{
			return;
		}

		Frames++;
		if (Angle >= 360f)
		{
			// The hand comes back to where the wrap starts, which is the game's own finish condition.
			Angle = 0f;
		}

		var radians = Angle * Mathf.Deg2Rad;
		var hand = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * Radius;
		var game = Traverse.Create(__instance).Field("game").GetValue();
		if (game == null)
		{
			return;
		}

		Traverse.Create(game).Field("handPos").SetValue(hand);
		Angle += Step;
		if (Angle > 360f)
		{
			Angle = 360f;
		}
	}
}

