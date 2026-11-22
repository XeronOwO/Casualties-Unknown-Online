using System;
using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.Acceptance
{
	/// <summary>
	/// Drives the bandage wrap's hand the way the platform pointer drives it: the hand is placed on the ring one
	/// step at a time, and every other condition of the wrap — the click-start window, the twenty-degree ratchet,
	/// the hand coming back under ninety degrees to finish — stays the game's own.
	///
	/// Why this exists (2026-11-23): the game computes the wrap from `Minigame.game.handPos`, and that hand is a
	/// damped body pulled at the platform pointer (`MinigameBase.UpdateHandPhysics`), so a staged pointer turns the
	/// roll but never carries the hand — measured on the real pair, the wrap advanced one twenty-degree step in a
	/// whole gesture and never finished. The suite is forbidden OS input and a hand written once per recipe call is
	/// overwritten by the next frame's physics; a per-frame placement is the only path that leaves the wrap's own
	/// conditions in the game's hands.
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
			Step = step > 0f ? step : 2f;
			Angle = 0f;
			Frames = 0;
			Active = true;
		}

		public static void Stop() => Active = false;

		/// <summary>Places the hand on the ring before the wrap reads it, and walks one step a frame.</summary>
		private static void BeforePhysicsUpdate()
		{
			if (!Active)
			{
				return;
			}

			// The hand lives on the shared minigame body (`MinigameBase.handPos`), reached through the game's own
			// static holder (`Minigame.game`); the wrap itself only reads it.
			var holder = AccessTools.TypeByName("Minigame");
			var shared = holder == null ? null : AccessTools.Field(holder, "game");
			var body = shared == null ? null : shared.GetValue(null);
			if (body == null)
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
			Traverse.Create(body).Field("handPos").SetValue(hand);
			Angle += Step;
			if (Angle > 360f)
			{
				Angle = 360f;
			}
		}
	}
}
