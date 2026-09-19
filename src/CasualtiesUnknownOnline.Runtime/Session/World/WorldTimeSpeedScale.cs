using System;
using CasualtiesUnknownOnline.Runtime.Protocol;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The one place that knows which <c>Time.timeScale</c> a
/// <see cref="WorldTimeSpeed"/> runs at — the multipliers mirror the game's own
/// <c>PlayerCamera.SetTimeScale</c> switch (1 / 5 / 20 / 25 / 3.5;
/// reversing/Assembly-CSharp/Assembly-CSharp/PlayerCamera.cs:663-690). Pure: no
/// Unity, no clock. The Game Adapter maps the game's <c>SpeedType</c> to
/// <see cref="WorldTimeSpeed"/> and reuses this for the numeric value, so the
/// two mappings cannot drift apart, and the correction ramp reads its endpoints
/// from the same place.
/// </summary>
public static class WorldTimeSpeedScale
{
	/// <summary>The scale this speed runs the world at (Normal for anything outside the five synchronized speeds).</summary>
	public static float ToTimeScale(WorldTimeSpeed speed) => speed switch
	{
		WorldTimeSpeed.Fast => 5f,
		WorldTimeSpeed.SuperFast => 20f,
		WorldTimeSpeed.UnconsciousFast => 25f,
		WorldTimeSpeed.DyingFast => 3.5f,
		_ => 1f,
	};

	/// <summary>
	/// The domain speed a live <c>Time.timeScale</c> currently represents, or
	/// null when it is not a domain speed: Paused (0), Slowmo (0.16) and every
	/// value between two speeds (a correction ramp) stay local presentation.
	/// </summary>
	public static WorldTimeSpeed? FromTimeScale(float timeScale)
	{
		if (timeScale <= 0.1f)
		{
			return null; // Paused (0) and anything near zero
		}

		if (Math.Abs(timeScale - 1f) < 0.01f)
		{
			return WorldTimeSpeed.Normal;
		}

		if (Math.Abs(timeScale - 3.5f) < 0.05f)
		{
			return WorldTimeSpeed.DyingFast;
		}

		if (Math.Abs(timeScale - 5f) < 0.05f)
		{
			return WorldTimeSpeed.Fast;
		}

		if (Math.Abs(timeScale - 20f) < 0.2f)
		{
			return WorldTimeSpeed.SuperFast;
		}

		if (Math.Abs(timeScale - 25f) < 0.25f)
		{
			return WorldTimeSpeed.UnconsciousFast;
		}

		return null;
	}
}
