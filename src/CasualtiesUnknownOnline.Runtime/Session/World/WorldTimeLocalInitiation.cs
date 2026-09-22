using CasualtiesUnknownOnline.Runtime.Protocol;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>One ramp step: the <c>Time.timeScale</c> to write, and whether the ramp reached its target.</summary>
public readonly record struct WorldTimeRampStep(float TimeScale, bool Done);

/// <summary>
/// The initiator-side half of the world-time model (user ruling 2026-09-18,
/// decision 184): a manual speed change takes effect on the player's OWN client
/// at once — the native SetTimeScale runs and the intent is reported — and the
/// host's answer settles it. While an intent is pending the client is
/// deliberately ahead of the shared clock, so enforcement of the host's last
/// value is suspended (<see cref="SuspendsEnforcement"/>); when the answer
/// differs, the clock returns to the host's value over
/// <see cref="RampSeconds"/> instead of snapping. Pure: no Unity, no clock —
/// the caller passes its live clock in and applies what this returns.
/// </summary>
public sealed class WorldTimeLocalInitiation : ISessionReset
{
	/// <summary>
	/// The correction ramp's length in unscaled seconds. It is a presentation
	/// constant, not a judgment window: long enough that the return reads as a
	/// ramp rather than a jolt, short enough to read as immediate, and measured
	/// in unscaled time so the ramp is not driven by the very clock it moves.
	/// </summary>
	public const float RampSeconds = 0.4f;

	/// <summary>
	/// The local clock's relationship to the shared one. Modelled as one state
	/// (not a set of bool flags) so the three cases cannot contradict each other.
	/// </summary>
	private enum Phase
	{
		Synced,
		Pending,
		Ramping,
	}

	private Phase _phase = Phase.Synced;
	private WorldTimeSpeed _authoritative = WorldTimeSpeed.Normal;
	private WorldTimeSpeed _pending = WorldTimeSpeed.Normal;
	private WorldTimeSpeed _rampTarget = WorldTimeSpeed.Normal;
	private float _rampFrom;
	private float _rampTo;
	private float _rampElapsed;

	/// <summary>The host's last authoritative speed — what this client runs with no local initiation in flight.</summary>
	public WorldTimeSpeed Authoritative => _authoritative;

	/// <summary>True while a locally applied intent waits for the host's answer.</summary>
	public bool IsPending => _phase == Phase.Pending;

	/// <summary>The locally applied intent awaiting the host's answer, or null when none is in flight.</summary>
	public WorldTimeSpeed? Pending => _phase == Phase.Pending ? _pending : null;

	/// <summary>True while the local clock is being corrected to the host's value.</summary>
	public bool IsRamping => _phase == Phase.Ramping;

	/// <summary>True while the local clock is deliberately not the host's value, so enforcing it would fight the player's own operation.</summary>
	public bool SuspendsEnforcement => _phase != Phase.Synced;

	/// <summary>
	/// A manual speed call (a speed hotkey, or the native left/right movement
	/// reset) reached this guest's SetTimeScale hook: the caller lets the local
	/// write run and reports the intent to the host. An intent equal to the
	/// host's last value needs no reconciliation and no suspension.
	/// </summary>
	public void BeginLocalInitiation(WorldTimeSpeed speed)
	{
		if (!WorldTimePolicy.IsSynchronizedSpeed(speed))
		{
			return;
		}

		_pending = speed;
		_phase = speed == _authoritative ? Phase.Synced : Phase.Pending;
	}

	/// <summary>
	/// The host's authoritative speed arrived. <paramref name="currentTimeScale"/>
	/// is this client's live clock, so a ramp that is already running restarts
	/// from where it actually is.
	/// </summary>
	public WorldTimeReconcile OnAuthoritative(WorldTimeSpeed speed, float currentTimeScale)
	{
		if (!WorldTimePolicy.IsSynchronizedSpeed(speed))
		{
			return WorldTimeReconcile.None;
		}

		var previous = _authoritative;
		_authoritative = speed;

		if (_phase == Phase.Pending)
		{
			if (speed == _pending)
			{
				_phase = Phase.Synced; // the host accepted the intent this client already runs
				return WorldTimeReconcile.None;
			}

			BeginRamp(speed, currentTimeScale); // refused or overridden — return to the host's value
			return WorldTimeReconcile.Ramp;
		}

		if (_phase == Phase.Ramping)
		{
			if (speed == _rampTarget)
			{
				return WorldTimeReconcile.None; // the ramp already heads there
			}

			BeginRamp(speed, currentTimeScale);
			return WorldTimeReconcile.Ramp;
		}

		return speed == previous ? WorldTimeReconcile.None : WorldTimeReconcile.Adopt;
	}

	/// <summary>
	/// Advances a running ramp by one unscaled frame and returns the clock value
	/// to write; <c>Done</c> means the value is the host's own and the caller
	/// should apply it through the normal SetTimeScale path (HUD + sound).
	/// </summary>
	public WorldTimeRampStep AdvanceRamp(float unscaledDeltaSeconds)
	{
		if (_phase != Phase.Ramping)
		{
			return default;
		}

		_rampElapsed += unscaledDeltaSeconds;
		var progress = _rampElapsed >= RampSeconds ? 1f : _rampElapsed / RampSeconds;
		var timeScale = _rampFrom + ((_rampTo - _rampFrom) * progress);
		if (progress < 1f)
		{
			return new WorldTimeRampStep(timeScale, false);
		}

		_phase = Phase.Synced;
		return new WorldTimeRampStep(_rampTo, true);
	}

	/// <summary>Session end / host departure: nothing is in flight any more and the shared clock is Normal.</summary>
	public void ResetSessionState()
	{
		_phase = Phase.Synced;
		_authoritative = WorldTimeSpeed.Normal;
		_pending = WorldTimeSpeed.Normal;
		_rampTarget = WorldTimeSpeed.Normal;
		_rampFrom = 0f;
		_rampTo = 0f;
		_rampElapsed = 0f;
	}

	private void BeginRamp(WorldTimeSpeed target, float currentTimeScale)
	{
		_phase = Phase.Ramping;
		_rampTarget = target;
		_rampFrom = currentTimeScale;
		_rampTo = WorldTimeSpeedScale.ToTimeScale(target);
		_rampElapsed = 0f;
	}
}
