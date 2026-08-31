using System;

namespace CasualtiesUnknownOnline.Runtime.Session.Commands;

/// <summary>
/// Pure alpha calculation for console lines: full opacity during the hold
/// period, then a linear fade to zero. Kept in Runtime (no Unity dependency) so
/// the UI can drive the same policy from deterministic unit tests and a real
/// wall clock.
/// </summary>
public static class ConsoleFadePolicy
{
	public static float ComputeAlpha(TimeSpan age, TimeSpan hold, TimeSpan fade)
	{
		if (age <= hold)
		{
			return 1f;
		}

		var remaining = fade - (age - hold);
		if (remaining <= TimeSpan.Zero)
		{
			return 0f;
		}

		return (float)(remaining.TotalMilliseconds / fade.TotalMilliseconds);
	}
}
