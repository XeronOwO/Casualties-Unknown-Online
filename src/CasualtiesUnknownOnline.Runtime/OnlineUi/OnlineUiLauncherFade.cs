namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// The Online UI launcher's idle fade: the top-right launcher is opaque while
/// the player is using it and settles to a translucent floor after an idle
/// window, so an untouched launcher stops covering the play area. The rule owns
/// the last-activity stamp and derives the alpha from (now, hovered) alone, so
/// every draw pass on the same frame returns the same value: an IMGUI frame
/// draws a Layout pass and a Repaint pass, and a rule that accumulated deltas
/// would count the idle time twice and flicker between the passes. Kept in
/// Runtime (no Unity dependency) for the same reason
/// <c>ConsoleFadePolicy</c> is — the UI drives it from the runtime clock and
/// the tests evaluate it directly.
/// </summary>
public sealed class OnlineUiLauncherFade
{
	/// <summary>How long the launcher stays fully opaque after the last hover or its first draw.</summary>
	public const long IdleDelayMs = 4_000;

	/// <summary>The linear ramp from full opacity down to <see cref="IdleAlpha"/>.</summary>
	public const long FadeMs = 600;

	/// <summary>The translucent floor: dark enough to read as UI, thin enough to see the world behind it.</summary>
	public const float IdleAlpha = 0.35f;

	private long _lastActiveMs;

	private bool _started;

	/// <summary>
	/// The launcher's opacity for this draw pass. A hover is activity: it returns
	/// full opacity and restarts the idle window. An untouched launcher starts its
	/// idle window at the first evaluation, so it fades without ever being pointed
	/// at; a clock that moved backwards (the runtime clock wraps) is treated as
	/// activity rather than as an enormous idle age.
	/// </summary>
	public float Evaluate(long nowMs, bool hovered)
	{
		if (!_started || hovered || nowMs < _lastActiveMs)
		{
			_started = true;
			_lastActiveMs = nowMs;
			return 1f;
		}

		var age = nowMs - _lastActiveMs;
		if (age <= IdleDelayMs)
		{
			return 1f;
		}

		var fadeAge = age - IdleDelayMs;
		if (fadeAge >= FadeMs)
		{
			return IdleAlpha;
		}

		return 1f - ((1f - IdleAlpha) * ((float)fadeAge / FadeMs));
	}
}
