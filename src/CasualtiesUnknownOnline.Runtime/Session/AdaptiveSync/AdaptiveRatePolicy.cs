using System;

namespace CasualtiesUnknownOnline.Runtime.Session.AdaptiveSync;

/// <summary>
/// Pure policy: maps a stream profile plus the current pressure level to an
/// effective cadence. Only <see cref="AdaptiveStreamDeliveryMode.LatestWins"/>
/// streams are adapted in Stage 1; reliable control and cumulative streams
/// keep their configured cadence (cumulative coalescing is a later stage).
/// </summary>
public sealed class AdaptiveRatePolicy
{
	/// <summary>
	/// Returns the effective Hz for a stream. <paramref name="baseHz"/> is the
	/// user/global configured cadence. For adapted <c>LatestWins</c> streams
	/// the result is clamped to the profile min/max; non-adapted streams
	/// preserve the base cadence exactly. When the profile has a non-zero
	/// <c>MaxBytesPerSecond</c> byte budget and <paramref name="averageFrameBytes"/>
	/// is known, the result is also capped by that budget.
	/// </summary>
	public int GetEffectiveHz(
		AdaptiveStreamProfile profile,
		AdaptivePressureLevel pressure,
		int baseHz,
		double averageFrameBytes = 0)
	{
		if (profile.DeliveryMode != AdaptiveStreamDeliveryMode.LatestWins)
		{
			// Reliable control and cumulative streams are not adapted by this
			// policy: preserve the exact configured cadence (cumulative
			// coalescing is a later stage), never clamp to adaptive bounds.
			return baseHz;
		}

		var factor = PressureFactor(pressure, profile.Priority);
		var effective = (int)Math.Round(baseHz * factor);

		if (profile.MaxBytesPerSecond > 0 && averageFrameBytes > 0)
		{
			var maxHzByBytes = (int)(profile.MaxBytesPerSecond / averageFrameBytes);
			effective = Math.Min(effective, maxHzByBytes);
		}

		return Clamp(effective, profile.MinHz, profile.MaxHz);
	}

	private static int Clamp(int value, int min, int max) =>
		Math.Max(min, Math.Min(max, value));

	private static float PressureFactor(AdaptivePressureLevel pressure, int priority)
	{
		return pressure switch
		{
			AdaptivePressureLevel.Optimal => 1f,
			AdaptivePressureLevel.Moderate => priority switch
			{
				1 => 0.75f,
				2 => 0.60f,
				_ => 0.50f,
			},
			AdaptivePressureLevel.High => priority switch
			{
				1 => 0.50f,
				2 => 0.35f,
				_ => 0.25f,
			},
			AdaptivePressureLevel.Critical => priority switch
			{
				1 => 0.25f,
				2 => 0.15f,
				_ => 0.10f,
			},
			_ => 1f,
		};
	}
}
