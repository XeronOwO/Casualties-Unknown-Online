namespace CasualtiesUnknownOnline.Runtime.OnlineUi;

/// <summary>
/// When the probe should try again, and when it is done. Two facts shape it: the game's canvas does not
/// exist during startup and appears within seconds of a launch, while the one unknown that needs a
/// RENDERED game (the canvas scale) can be an hour of menu away — so a single deadline measured from the
/// plugin's load would throw the whole reading away on an ordinary session. The policy therefore polls
/// fast while the canvas is still missing (bounded by a surface deadline, because a game that never
/// reaches a menu must not be polled forever), slows to a long interval once the canvas is there, and
/// keeps asking until every unknown is read or the attempt budget runs out.
///
/// <para>
/// Nothing here touches Unity or a clock — the caller passes the timestamp — which is what makes every
/// path a unit case. The clock may wrap (<c>Environment.TickCount</c>); a backwards stamp restarts the
/// interval and the surface window instead of reading as an enormous age, and the attempt budget stays
/// the hard bound.
/// </para>
/// </summary>
public sealed class OnlineUiNativeFactsCapturePolicy(
	int fastIntervalMs,
	int slowIntervalMs,
	int surfaceDeadlineMs,
	int maxAttempts)
{
	/// <summary>How often the probe polls while the game has no canvas yet.</summary>
	public const int DefaultFastIntervalMs = 500;

	/// <summary>How often it polls once the canvas exists but an unknown is still unread — long enough that
	/// a whole session of waiting costs a handful of calls.</summary>
	public const int DefaultSlowIntervalMs = 5_000;

	/// <summary>How long a game may go without ever producing a canvas before the probe reports it missing.</summary>
	public const int DefaultSurfaceDeadlineMs = 300_000;

	/// <summary>The attempt ceiling: the hard bound for a canvas that exists but never becomes readable.</summary>
	public const int DefaultMaxAttempts = 2_000;

	private readonly int _fastIntervalMs = fastIntervalMs;
	private readonly int _slowIntervalMs = slowIntervalMs;
	private readonly int _surfaceDeadlineMs = surfaceDeadlineMs;
	private readonly int _maxAttempts = maxAttempts;
	private long _startedMs;
	private long _lastAttemptMs;
	private bool _started;
	private bool _attached;

	/// <summary>The budget the product's probe runs with: half a second while the game boots, five seconds
	/// afterwards, five minutes for a canvas to appear, and an attempt ceiling that bounds a session.</summary>
	public static OnlineUiNativeFactsCapturePolicy Default() =>
		new(DefaultFastIntervalMs, DefaultSlowIntervalMs, DefaultSurfaceDeadlineMs, DefaultMaxAttempts);

	/// <summary>Where the run stands; the caller logs once when this stops being <see cref="OnlineUiNativeFactsOutcome.Pending"/>.</summary>
	public OnlineUiNativeFactsOutcome Outcome { get; private set; } = OnlineUiNativeFactsOutcome.Pending;

	/// <summary>How many attempts were made — part of the reading, so a partial result says how hard the probe tried.</summary>
	public int Attempts { get; private set; }

	/// <summary>True once an attempt saw the game's canvas.</summary>
	public bool Attached => _attached;

	/// <summary>
	/// True while the run is unfinished and the interval since the last attempt has passed. The interval
	/// is the fast one until the canvas has been seen and the slow one afterwards; a clock that went
	/// backwards is due immediately rather than stalled until the counter catches up.
	/// </summary>
	public bool ShouldAttempt(long nowMs) =>
		Outcome == OnlineUiNativeFactsOutcome.Pending
		&& (!_started
			|| nowMs < _lastAttemptMs
			|| nowMs - _lastAttemptMs >= IntervalMs);

	private int IntervalMs => _attached ? _slowIntervalMs : _fastIntervalMs;

	/// <summary>
	/// Records one attempt and its reading and returns the outcome the run is in now: still
	/// <see cref="OnlineUiNativeFactsOutcome.Pending"/>, or the terminal state the caller reports once.
	/// A complete reading finishes immediately; a reading that is still missing something keeps asking —
	/// with no deadline of its own once the canvas exists, because the game may simply not have started a
	/// run yet — until the attempt budget runs out, which reports <see cref="OnlineUiNativeFactsOutcome.Partial"/>
	/// when a canvas was seen and <see cref="OnlineUiNativeFactsOutcome.SurfaceMissing"/> when none ever was.
	/// </summary>
	public OnlineUiNativeFactsOutcome NoteAttempt(OnlineUiNativeFacts facts, long nowMs)
	{
		if (Outcome != OnlineUiNativeFactsOutcome.Pending)
		{
			return Outcome;
		}

		if (!_started || nowMs < _lastAttemptMs)
		{
			// A wrapped runtime clock restarts the window instead of reading as an enormous age; the
			// attempt budget is still what bounds the run.
			_started = true;
			_startedMs = nowMs;
		}

		_lastAttemptMs = nowMs;
		Attempts++;

		if (facts.IsComplete)
		{
			return Finish(OnlineUiNativeFactsOutcome.Complete);
		}

		if (facts.CanvasAttached)
		{
			_attached = true;
		}

		if (Attempts >= _maxAttempts)
		{
			return Finish(_attached
				? OnlineUiNativeFactsOutcome.Partial
				: OnlineUiNativeFactsOutcome.SurfaceMissing);
		}

		if (!_attached && nowMs - _startedMs >= _surfaceDeadlineMs)
		{
			return Finish(OnlineUiNativeFactsOutcome.SurfaceMissing);
		}

		return OnlineUiNativeFactsOutcome.Pending;
	}

	private OnlineUiNativeFactsOutcome Finish(OnlineUiNativeFactsOutcome outcome)
	{
		Outcome = outcome;
		return outcome;
	}
}
