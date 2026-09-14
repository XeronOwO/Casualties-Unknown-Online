namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// One-shot pending menu-return request: the mode (does this return cut?) and the
/// origin (who asked?). Session teardown events and the game's own scene-load
/// interception both run outside the pump — Unity scene loads must happen on the
/// normal Update pump — so the intent is recorded here and the seam consumes it on
/// the next frame.
/// </summary>
internal sealed class RunMenuReturnRequest
{
	private bool _pending;
	private RunMenuReturnMode _mode;
	private RunMenuReturnOrigin _origin;

	internal bool IsPending => _pending;

	/// <summary>The requested mode without consuming it — the pump decides whether the world can be left yet (a deferred cut keeps the request pending).</summary>
	internal RunMenuReturnMode Pending => _pending ? _mode : RunMenuReturnMode.None;

	/// <summary>Who asked for the pending return (meaningless while nothing is pending).</summary>
	internal RunMenuReturnOrigin Origin => _origin;

	/// <summary>The request is done with: the world was left, or a new session made it stale.</summary>
	internal void Clear()
	{
		_pending = false;
		_mode = RunMenuReturnMode.None;
	}

	internal void Request(RunMenuReturnMode mode, RunMenuReturnOrigin origin)
	{
		if (mode == RunMenuReturnMode.None)
		{
			return;
		}

		_pending = true;
		_mode = mode;
		_origin = origin;
	}
}
