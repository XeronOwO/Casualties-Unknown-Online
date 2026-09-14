using CasualtiesUnknownOnline.Runtime.Session;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter.Run;

/// <summary>
/// Owns the deferred menu return: the record that "the world is about to be
/// left" and the one place the scene load is actually performed.
///
/// Two kinds of caller ask for it, and both run outside the pump — which is why
/// the leave is deferred at all: a session teardown event (inside a Steam/UI
/// callback) and the game's own <c>PlayerCamera.ToMainMenu</c>, the single funnel
/// every deliberate "leave the world" action goes through (the pause quit, the
/// console's <c>saveandquit</c>, the tutorial exit). The second one is what gives
/// SOLO play a menu-exit trigger: it has no session teardown to ride, and letting
/// the scene load run would destroy the world before the cut could read it.
///
/// It holds the REQUEST only. The pump that acts on it is
/// <see cref="SaveCutSeam"/>, because the deliberate return is itself a mid-run
/// cut taken at the same frame-end seam: the cut and the leave are one operation,
/// and a cut the transient policy defers makes the leave wait a frame.
///
/// The cut it persists is the CUO world archive's (decision 164): the native
/// <c>SaveSystem.SaveGame()</c> that used to run here is gone, because decision
/// 165 makes CUO independent of <c>save.sv</c> and two writers would be two
/// sources of truth. The trigger itself stays — "the player deliberately leaves
/// the world" is the one cut the player asks for explicitly.
/// </summary>
internal sealed class RunMenuReturnCoordinator(ILogger log)
{
	private readonly RunMenuReturnRequest _request = new();
	private readonly ILogger _log = log;
	private bool _replayingLeave;

	internal bool IsPending => _request.IsPending;

	/// <summary>
	/// True while THIS class is performing the leave it recorded. The scene-load
	/// interception asks before deferring: the load it is about to let through is
	/// the recorded request being honoured, and deferring it again would make the
	/// world un-leavable (the request is cleared by the time the load runs).
	/// </summary>
	internal bool IsReplayingLeave => _replayingLeave;

	/// <summary>The requested mode (a peek — the request is only cleared when the leave actually happens, or when a new session makes it stale).</summary>
	internal RunMenuReturnMode Pending => _request.Pending;

	/// <summary>Who asked for the pending return (the seam's staleness rule depends on it).</summary>
	internal RunMenuReturnOrigin Origin => _request.Origin;

	/// <summary>
	/// Record the intent to leave the world (never load a scene here). Called from
	/// a session-teardown event and from the scene-load interception of a
	/// deliberate leave; both are outside the pump, and <paramref name="inWorld"/>
	/// is the caller's answer to "is there a world this return would leave".
	/// <paramref name="origin"/> rides along: the seam's staleness rule depends on
	/// who asked (a teardown belongs to the session that is ending; a player's own
	/// leave does not).
	/// </summary>
	internal void Request(SessionRole role, RunMenuReturnOrigin origin, bool inWorld)
	{
		var mode = RunMenuReturnPolicy.Decide(role, inWorld);
		if (mode == RunMenuReturnMode.None)
		{
			return;
		}

		_request.Request(mode, origin);
		if (mode == RunMenuReturnMode.SaveAndMenu)
		{
			_log.LogInformation("In the world with the save authority — cutting the run into the CUO world archive and returning to the main menu on the next pump.");
		}
		else
		{
			_log.LogInformation("In the world without the save authority (a guest) — returning to the main menu on the next pump, writing nothing.");
		}
	}

	/// <summary>The request is done with (the world was left, or a new session superseded it).</summary>
	internal void Clear() => _request.Clear();

	/// <summary>
	/// Leave the world for the main menu. The caller (<see cref="SaveCutSeam"/>)
	/// has already taken the cut — or decided there is nothing to cut — and checked
	/// that the camera exists. <c>false</c> = the leave did not happen (the camera
	/// vanished between that check and this call), so the request must stay armed
	/// and the leave be retried.
	/// </summary>
	internal bool Leave()
	{
		var camera = PlayerCamera.main;
		if (camera == null) // Unity object — ==
		{
			_log.LogWarning("The world was left without a scene load: the player camera is gone.");
			return false;
		}

		_log.LogInformation("Leaving the world to the main menu.");
		_replayingLeave = true;
		try
		{
			camera.ToMainMenu();
		}
		finally
		{
			_replayingLeave = false;
		}

		return true;
	}
}
