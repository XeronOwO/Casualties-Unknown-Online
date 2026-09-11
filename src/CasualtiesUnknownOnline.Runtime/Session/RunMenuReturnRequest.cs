namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// One-shot pending menu-return request. Session teardown events run inside
/// Steam/UI callbacks; Unity scene loads must happen on the normal Update pump,
/// so the teardown records intent here and the adapter consumes it on the next
/// frame instead of calling <c>SceneManager.LoadScene</c> synchronously.
/// </summary>
internal sealed class RunMenuReturnRequest
{
	private bool _pending;
	private RunMenuReturnMode _mode;

	internal bool IsPending => _pending;

	/// <summary>The requested mode without consuming it — the pump decides whether the world can be left yet (a deferred cut keeps the request pending).</summary>
	internal RunMenuReturnMode Pending => _pending ? _mode : RunMenuReturnMode.None;

	/// <summary>The request is done with: the world was left, or a new session made it stale.</summary>
	internal void Clear()
	{
		_pending = false;
		_mode = RunMenuReturnMode.None;
	}

	internal void Request(RunMenuReturnMode mode)
	{
		if (mode == RunMenuReturnMode.None)
		{
			return;
		}

		_pending = true;
		_mode = mode;
	}

	internal bool TryConsume(out RunMenuReturnMode mode)
	{
		mode = _mode;
		if (!_pending)
		{
			return false;
		}

		_pending = false;
		return true;
	}
}
