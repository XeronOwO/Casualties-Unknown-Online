using System;
using System.Threading;
using HarmonyLib;
using UDebug = UnityEngine.Debug;

namespace CasualtiesUnknownOnline.Acceptance;

// Counts the swings the game actually makes, so a row that holds the attack can tell "the game never swung" apart from
// "the swing missed". `PlayerCamera.HandleAttacks` reaches the swing through `Body.UseItemInHand`
// (PlayerCamera.cs:1995-2014), and an empty hand ends in `DoUnarmedAttack` (Body.cs:2162-2168); counting that call is the
// narrowest seam that answers the question without changing behaviour.
public static class SwingProbePatch
{
	private static long _swings;

	public static long Swings => Interlocked.Read(ref _swings);

	public static void Reset() => Interlocked.Exchange(ref _swings, 0);

	public static void Install(Harmony harmony)
	{
		if (harmony is null)
		{
			throw new ArgumentNullException(nameof(harmony));
		}

		var body = AccessTools.TypeByName("Body");
		var swing = body is null ? null : AccessTools.Method(body, "DoUnarmedAttack");
		if (swing is null)
		{
			UDebug.LogWarning("[Acceptance] the swing counter has no seam on this build; a row will not be able to say " +
				"whether the game swung, only that the block never took damage.");
			return;
		}

		harmony.Patch(swing, prefix: new HarmonyMethod(typeof(SwingProbePatch), nameof(Count)));
	}

	private static void Count() => Interlocked.Increment(ref _swings);
}
