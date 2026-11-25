using System;
using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using UnityEngine;

namespace CasualtiesUnknownOnline.Acceptance;

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

		// The frame the wrap owes its state to: `MinigameBase.Update` sets `handStartedClicking` and then runs the
		// current minigame's `Update`, which copies `holdingBandage` on that frame only. Reading the two fields after
		// the update is the only way to see whether the frame the edge landed on was the frame the wrap read
		// (measured 2026-11-26: the row's samples never showed `handStarted`, while the channel answered the DOWN).
		var updateHolder = AccessTools.TypeByName("MinigameBase");
		var updateMethod = updateHolder == null ? null : AccessTools.Method(updateHolder, "UpdateHandVisuals");
		if (updateMethod != null)
		{
			harmony.Patch(updateMethod, postfix: new HarmonyMethod(typeof(BandageHandDrive), nameof(AfterMinigameBaseUpdate)));
		}
		_harmony = harmony;
	}

	public static void Start(float radius, float lead)
	{
		Radius = radius > 1f ? radius : 250f;
		// A negative step is a small lead, not a missing one: `|step|` is how far ahead of the roll the hand sits each
		// frame, and the roll advances by about that much. Twenty degrees stepped the roll from 345 straight past its
		// own finish window (`bandageAngle > 350 && hand < 90`), so the wrap reset without ever completing (measured
		// 2026-11-26, band-34: `roll345 hand360`, then `roll0 hand15` — the roll wrapped and no wrap was taken).
		Lead = lead < 0f ? Mathf.Abs(lead) : lead > 0f ? lead : 20f;
		Angle = 0f;
		Frames = 0;
		Active = true;
		lock (UpdateTrace)
		{
			UpdateTrace.Clear();
			UpdateTrace.Enqueue("start");
		}
	}

	public static void Stop()
	{
		Active = false;
		Debug.Log("[BandageFrame] the wrap's own frames: " + UpdateTraceText);
	}

	/// <summary>Places the hand on the ring ahead of the roll before the wrap reads it.</summary>
	private static void BeforePhysicsUpdate()
	{
		if (!Active)
		{
			return;
		}

		lock (UpdateTrace)
		{
			UpdateTrace.Enqueue("pre");
			while (UpdateTrace.Count > 64)
			{
				UpdateTrace.Dequeue();
			}
		}

		try
		{
			Place();
		}
		catch (Exception exception)
		{
			// A throw inside a Harmony prefix is logged on every frame; the drive stops instead and the case's own
			// reading reports the untouched hand. The reason also goes into the frame trace, because a Unity warning
			// does not reach the row's report (measured 2026-11-26: the drive stopped on its first frame and every
			// reading looked like the game had simply ignored the hand).
			Active = false;
			lock (UpdateTrace)
			{
				UpdateTrace.Enqueue("err:" + exception.GetType().Name + ":" + exception.Message);
			}

			Debug.LogWarning("[Acceptance] the bandage hand drive stopped: " + exception.Message);
		}
	}

	private static void Place()
	{
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
		// The hand walks the lap the way a circling hand does: it advances by the step on every physics pass, so the
		// hand keeps moving relative to the roll and the roll follows it. Placing it at `roll + lead` on every pass held
		// the hand still relative to the roll, and the roll parked at 300 with `holding` still true (measured
		// 2026-11-26: `roll195hand210` … `roll300hand315`, bleed 24.5 to 20.6 and never stopped).
		Angle = (Angle + Lead) % 360f;

		var radians = Angle * Mathf.Deg2Rad;
		// The game's own angle is measured with `Mathf.Atan2(handPos.x, handPos.y)` (the row's `handAngle`), so a hand at
		// the roll's own degrees is (sin, cos), NOT (cos, sin): placing the plain cosine pair put the hand a quarter turn
		// away from the roll it was told to sit on (measured 2026-11-26: placed at 15 degrees, read back as 75, and the
		// window's dot product was sin(15) instead of cos(15)).
		var hand = new Vector2(Mathf.Sin(radians), Mathf.Cos(radians)) * Radius;
		Traverse.Create(body).Field("handPos").SetValue(hand);

		// The native physics pulls the hand for the rest of the frame (`BandageMinigame.cs:129-130`:
		// `handVelocity += handPos.normalized * deltaTime`, then `handPos = handPos.normalized * 250f`), so a hand placed
		// here and left with its old velocity drifts off the placement before the wrap reads it. Zeroing the velocity is
		// what makes the placed angle the angle the game sees (measured 2026-11-26: `handPos` was placed at the roll but
		// `handAngle` kept reading 75 for the whole gesture).
		Traverse.Create(body).Field("handVelocity").SetValue(Vector2.zero);

		// What the wrap saw this frame, read where the physics stage runs (after the frame's updates): the two flags
		// `MinigameBase.UpdateHandVisuals` sets and the state the item's minigame copied from them. This is the pairing
		// the row's own samples cannot show — a flag that is true for one frame is invisible to a 60 ms poll.
		var startedNow = Convert.ToBoolean(AccessTools.Field(holder, "handStartedClicking").GetValue(body));
		var holdingField = current == null ? null : AccessTools.Field(current.GetType(), "holdingBandage");
		var holdingNow = holdingField != null && Convert.ToBoolean(holdingField.GetValue(current));
		lock (UpdateTrace)
		{
			UpdateTrace.Enqueue(
				Time.frameCount.ToString(CultureInfo.InvariantCulture)
				+ "=roll" + roll.ToString("0", CultureInfo.InvariantCulture)
				+ "hand" + Angle.ToString("0", CultureInfo.InvariantCulture)
				+ (startedNow ? "s1" : "s0")
				+ (holdingNow ? "h1" : "h0"));
			while (UpdateTrace.Count > 64)
			{
				UpdateTrace.Dequeue();
			}
		}
	}

	/// <summary>
	/// What the wrap saw on each frame it ran, after the frame's own updates: the two flags the item's minigame reads
	/// and the state it copied from them.
	/// </summary>
	private static void AfterMinigameBaseUpdate()
	{
		if (!Active)
		{
			return;
		}

		try
		{
			var holder = AccessTools.TypeByName("MinigameBase");
			var body = holder == null ? null : AccessTools.Field(holder, "main").GetValue(null);
			if (body == null)
			{
				return;
			}

			var started = Convert.ToBoolean(AccessTools.Field(holder, "handStartedClicking").GetValue(body));
			var clicking = Convert.ToBoolean(AccessTools.Field(holder, "handClicking").GetValue(body));
			var current = AccessTools.Field(holder, "currentMinigame").GetValue(body);
			var holdingField = current == null ? null : AccessTools.Field(current.GetType(), "holdingBandage");
			var holding = holdingField != null && Convert.ToBoolean(holdingField.GetValue(current));

			lock (UpdateTrace)
			{
				UpdateTrace.Enqueue(
					Time.frameCount.ToString(CultureInfo.InvariantCulture)
					+ "=" + (clicking ? "c1" : "c0") + (started ? "s1" : "s0") + (holding ? "h1" : "h0"));
				while (UpdateTrace.Count > 64)
				{
					UpdateTrace.Dequeue();
				}
			}
		}
		catch (Exception exception)
		{
			Active = false;
			Debug.LogWarning("[Acceptance] the bandage frame trace stopped: " + exception.Message);
		}
	}

	private static readonly Queue<string> UpdateTrace = new();

	internal static string UpdateTraceText
	{
		get
		{
			lock (UpdateTrace)
			{
				return string.Join(";", UpdateTrace);
			}
		}
	}
}
