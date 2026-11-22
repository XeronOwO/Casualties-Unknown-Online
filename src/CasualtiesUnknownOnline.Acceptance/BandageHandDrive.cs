using System;
using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.Acceptance
{
	/// <summary>
	/// Drives the bandage wrap's hand the way a player's circling hand is seen by the game: the hand is placed on the
	/// ring a little ahead of the roll each frame, and every condition of the wrap — the click-start window, the
	/// twenty-degree ratchet, the finish as the hand comes back under ninety degrees — stays the game's own.
	///
	/// Why this exists (2026-11-23): the hand is a damped body pulled at the platform pointer
	/// (`MinigameBase.UpdateHandPhysics`), so a staged pointer turns the roll but never carries the hand — measured on
	/// the real pair, the wrap advanced one twenty-degree step in a whole gesture and never finished. The suite is
	/// forbidden OS input, and a hand written once per recipe call is overwritten by the next frame's physics.
	///
	/// The lead matters: `holdingBandage` is `dot(roll, hand) > 0.9` (`BandageMinigame.cs:81`), i.e. the hand must stay
	/// inside about twenty-five degrees of the roll. A hand walking at a constant speed falls out of that window as the
	/// roll jumps twenty degrees a step (`.acceptance/bandage-26`: `holding` true in nine samples of thirty-one, the
	/// fill stalling at 0.064). Placing the hand relative to the roll each frame keeps it inside the window until the
	/// roll passes three hundred and fifty degrees, where the hand comes back to the start and the wrap finishes.
	/// </summary>
	public static class BandageHandDrive
	{
		private static Harmony? _harmony;

		internal static bool Active { get; private set; }

		internal static float Radius { get; private set; }

		internal static float Lead { get; private set; }

		internal static float Angle { get; private set; }

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

		public static void Start(float radius, float lead)
		{
			Radius = radius > 1f ? radius : 250f;
			Lead = lead > 0f ? lead : 20f;
			Angle = 0f;
			Frames = 0;
			Active = true;
		}

		public static void Stop() => Active = false;

		/// <summary>Places the hand on the ring ahead of the roll before the wrap reads it.</summary>
		private static void BeforePhysicsUpdate()
		{
			if (!Active)
			{
				return;
			}

			// The hand lives on the shared minigame body (`MinigameBase.handPos`); the roll is the wrap's own field,
			// reachable through the same body's `currentMinigame`.
			var holder = AccessTools.TypeByName("MinigameBase");
			var shared = holder == null ? null : AccessTools.Field(holder, "main");
			var body = shared == null ? null : shared.GetValue(null);
			if (body == null)
			{
				return;
			}

			var currentField = AccessTools.Field(holder, "currentMinigame");
			var current = currentField == null ? null : currentField.GetValue(body);
			var rollField = current == null ? null : AccessTools.Field(current.GetType(), "bandageAngle");
			var roll = rollField == null ? 0f : Convert.ToSingle(rollField.GetValue(current));

			Frames++;
			// The hand sits just ahead of the roll while it turns, and comes back to the start once the roll is nearly
			// done — that return is the game's own finish condition (`bandageAngle > 350 && angle < 90`).
			Angle = roll > 350f ? 0.5f : roll + Lead;

			var radians = Angle * Mathf.Deg2Rad;
			var hand = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * Radius;
			Traverse.Create(body).Field("handPos").SetValue(hand);
		}
	}
}
