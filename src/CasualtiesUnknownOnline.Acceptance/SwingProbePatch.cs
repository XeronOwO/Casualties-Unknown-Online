using System;
using System.Threading;
using HarmonyLib;
using UnityEngine;
using UDebug = UnityEngine.Debug;

namespace CasualtiesUnknownOnline.Acceptance;

// Counts the game's own swing entry points, so a row that holds the attack can tell "the game never swung" apart from
// "the swing missed". Two seams, because they sit on either side of the game's own branch: `PlayerCamera.HandleAttacks`
// reaches `Body.UseItemInHand` (PlayerCamera.cs:1995-2014), and an empty hand falls through to `Body.DoUnarmedAttack`
// (Body.cs:2718-2732). Counting both says whether the branch was taken at all.
public static class SwingProbePatch
{
	private static long _swings;
	private static long _uses;
	private static long _hurts;

	public static long Swings => Interlocked.Read(ref _swings);

	public static long Uses => Interlocked.Read(ref _uses);

	/// <summary>
	/// How many times the world took damage at all. It separates "the swing missed" from "the swing hit and the block under
	/// the cursor was not the one it hit" — the ray starts at the hand and needs the `BlockGround` tag (Body.cs:2162-2168).
	/// </summary>
	public static long Hurts => Interlocked.Read(ref _hurts);

	/// <summary>What the install did, in words: a row reads it to tell "the seam is missing" from "the game never swung".</summary>
	public static string Seam { get; private set; } = "not installed";

	public static void Reset()
	{
		Interlocked.Exchange(ref _swings, 0);
		Interlocked.Exchange(ref _uses, 0);
	}

	public static void Install(Harmony harmony)
	{
		if (harmony is null)
		{
			throw new ArgumentNullException(nameof(harmony));
		}

		var body = AccessTools.TypeByName("Body");
		if (body is null)
		{
			UDebug.LogWarning("[Acceptance] the swing counter found no Body type on this build; a row will not be able to " +
				"say whether the game swung, only that the block never took damage.");
			return;
		}

		var use = AccessTools.Method(body, "UseItemInHand");
		var swing = AccessTools.Method(body, "DoUnarmedAttack");
		if (use is null || swing is null)
		{
			UDebug.LogWarning("[Acceptance] the swing counter has no seam on this build (UseItemInHand: " +
				(use is null ? "missing" : "found") + ", DoUnarmedAttack: " + (swing is null ? "missing" : "found") +
				"); a row will not be able to say whether the game swung, only that the block never took damage.");
			return;
		}

		harmony.Patch(use, prefix: new HarmonyMethod(typeof(SwingProbePatch), nameof(CountUse)));
		harmony.Patch(swing, prefix: new HarmonyMethod(typeof(SwingProbePatch), nameof(CountSwing)));
		var world = AccessTools.TypeByName("WorldGeneration");
		// The game carries two DamageBlock overloads (Vector2Int and Vector2), so a name-only lookup throws
		// AmbiguousMatchException — and an exception here aborts the rest of the channel's install, which once cost the
		// evaluator its game-assembly reference and turned every recipe into CS0103 (measured 2026-11-22).
		var damage = world is null
			? null
			: AccessTools.Method(world, "DamageBlock", [typeof(Vector2), typeof(float), typeof(bool), typeof(bool)]);
		if (damage is not null)
		{
			harmony.Patch(damage, prefix: new HarmonyMethod(typeof(SwingProbePatch), nameof(CountHurt)));
		}

		Seam = "installed on " + body.FullName + (damage is null ? " (no DamageBlock seam)" : " and " + world!.FullName);
	}

	private static void CountUse() => Interlocked.Increment(ref _uses);

	private static void CountSwing() => Interlocked.Increment(ref _swings);

	private static void CountHurt() => Interlocked.Increment(ref _hurts);
}
