using System.Collections.Generic;
using System.Linq;

namespace CasualtiesUnknownOnline.GameAdapter.Run;

/// <summary>
/// The start-gate alert queue: PlayerCamera.DoAlert popups that fire while the
/// gate window holds are suppressed and replayed in order once the run is
/// playing. Pure presentation state — no Unity objects, no session state; the
/// window decision stays in RunCoordinator and the Unity write in
/// StartGateCoordinator.
/// </summary>
internal sealed class StartGateAlertQueue
{
	private readonly Queue<StartGateAlert> _pending = new();

	internal bool HasPending => _pending.Count > 0;

	/// <summary>Queue one suppressed popup. Returns true so the call site can
	/// express "deferred" in one statement.</summary>
	internal bool TryDefer(string text, bool important)
	{
		_pending.Enqueue(new StartGateAlert(text, important));
		return true;
	}

	/// <summary>Take the queued popups in capture order and empty the queue.</summary>
	internal IReadOnlyList<StartGateAlert> TakeAll()
	{
		var pending = _pending.ToList();
		_pending.Clear();
		return pending;
	}

	internal void Clear() => _pending.Clear();
}
