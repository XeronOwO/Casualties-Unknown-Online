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
