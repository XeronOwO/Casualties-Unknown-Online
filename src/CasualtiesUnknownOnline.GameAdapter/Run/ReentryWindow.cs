namespace CasualtiesUnknownOnline.GameAdapter.Run;

/// <summary>
/// The member's bounded re-entry window as a pure state machine: after the HOST's own world exit
/// pulled this member out of a world it was in, it asks the host to re-issue the entry
/// instruction, a bounded number of times, until it is back in a world or the window runs out.
/// Kept free of Unity and the session so every transition has a case in
/// <c>ReentryWindowTests</c> — the state that matters most is the first frame: the host's exit
/// arrives while the member is STILL inside its old world (the menu return is deferred to the
/// frame-end seam), so "in a world" must not mean "back" until this member has observed its own
/// unload. Treating it as "back" armed and dropped the window in the same frame, and the recovery
/// never asked at all.
/// <para>
/// The window is armed only when the host's exit is what takes this member out of a world it is
/// in, entering or generating — the arm's caller answers <c>memberInWorldOrEntering</c> from the
/// member's own run state. A member already sitting in the menu when the host exits left
/// deliberately: its menu is its own and the host's ordinary entry invite is not for it.
/// </para>
/// </summary>
internal sealed class ReentryWindow
{
	/// <summary>How often the window asks. The scene re-report rides 5 s too, and the host's own start-gate force-start is 30 s.</summary>
	internal const long IntervalMs = 5_000;

	/// <summary>Asks one window spends (12 × 5 s = 60 s): the host's 30 s gate force-start plus a world generation under load.</summary>
	internal const int MaxRequests = 12;

	/// <summary>The window's own lifetime: a host that stays in the menu ends the wait instead of leaving a live window armed.</summary>
	internal const long WindowMs = 180_000;

	private bool _armed;

	/// <summary>This member has seen itself out of the world since the window armed — the only fact that makes a later "in world" a return.</summary>
	private bool _wasOut;

	private long _armedMs;
	private long _lastAskMs;
	private int _asks;

	internal bool Armed => _armed;

	/// <summary>Asks this window has actually sent (spent by <see cref="NoteAsk"/>).</summary>
	internal int Asks => _asks;

	/// <summary>Why the window closed (set by <see cref="Step"/> when it answers <see cref="ReentryWindowState.Closed"/>).</summary>
	internal string CloseReason { get; private set; } = "";

	/// <summary>
	/// Arms on the host's world exit. False = this member was not in (or entering) a world when the
	/// host left, so its menu is its own and nothing here is owed to it. A second exit while the
	/// member is already out does not restart the window (the caller passes false and the existing
	/// budget stands); an exit after the member re-entered arms a fresh one.
	/// </summary>
	internal bool TryArm(long nowMs, bool memberInWorldOrEntering)
	{
		if (!memberInWorldOrEntering)
		{
			return false;
		}

		_armed = true;
		_wasOut = false;
		_armedMs = nowMs;
		_lastAskMs = 0;
		_asks = 0;
		CloseReason = "";
		return true;
	}

	/// <summary>
	/// One frame. The owner passes this member's own run facts and whether the host is (per this
	/// member's view) inside a world; the window decides whether to wait, to ask, or to close.
	/// </summary>
	internal ReentryWindowState Step(long nowMs, bool inWorld, bool generating, bool entering, bool hostInWorld)
	{
		if (!_armed)
		{
			return ReentryWindowState.Idle;
		}

		if (nowMs < _armedMs || (_lastAskMs != 0 && nowMs < _lastAskMs))
		{
			// The clock went backwards (Environment.TickCount wraps every ~24.9 days): restart the
			// window at the new reading instead of stalling on a deadline that is now centuries away.
			_armedMs = nowMs;
			_lastAskMs = 0;
			return ReentryWindowState.Waiting;
		}

		var inWorldOrGenerating = inWorld || generating;
		if (!inWorldOrGenerating && !entering)
		{
			_wasOut = true; // the pull really took this member out of its world
		}

		if (inWorldOrGenerating || entering)
		{
			// The arming frame is inside this branch on purpose: the member is still in the OLD world
			// while its menu return is deferred, and that is not a return. Only a member this window
			// has seen go out can come back.
			if (_wasOut && inWorld)
			{
				Close("this member is back in the world");
				return ReentryWindowState.Closed;
			}

			return ReentryWindowState.Waiting;
		}

		if (nowMs - _armedMs >= WindowMs)
		{
			Close($"the window closed after {WindowMs / 1000} s without a world entry");
			return ReentryWindowState.Closed;
		}

		if (_asks >= MaxRequests)
		{
			Close($"{MaxRequests} asks went unanswered — the host did not take this member back in");
			return ReentryWindowState.Closed;
		}

		if (!hostInWorld)
		{
			return ReentryWindowState.Waiting; // the host is not in a world to enter yet
		}

		if (_lastAskMs != 0 && nowMs - _lastAskMs < IntervalMs)
		{
			return ReentryWindowState.Waiting;
		}

		return ReentryWindowState.Ready;
	}

	/// <summary>The ask actually left this client — spend the cadence and one report of the budget.</summary>
	internal void NoteAsk(long nowMs)
	{
		_lastAskMs = nowMs;
		_asks++;
	}

	/// <summary>The session or the binding is gone — the window belongs to it.</summary>
	internal void Reset()
	{
		_armed = false;
		_wasOut = false;
		_asks = 0;
		_lastAskMs = 0;
		CloseReason = "";
	}

	private void Close(string reason)
	{
		_armed = false;
		_wasOut = false;
		CloseReason = reason;
	}
}
