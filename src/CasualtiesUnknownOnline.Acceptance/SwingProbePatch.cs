using System;
using System.Threading;
using HarmonyLib;
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

	public static long Swings => Interlocked.Read(ref _swings);

	public static long Uses => Interlocked.Read(ref _uses);

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
		Seam = "installed on " + body.FullName;
	}

	private static void CountUse() => Interlocked.Increment(ref _uses);

	private static void CountSwing() => Interlocked.Increment(ref _swings);
}
