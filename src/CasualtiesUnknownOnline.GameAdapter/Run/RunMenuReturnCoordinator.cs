using CasualtiesUnknownOnline.Runtime.Session;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter.Run;

/// <summary>
/// Owns the deferred post-session menu return. Session teardown events run
/// inside Steam/UI callbacks, so the scene load is recorded here and performed on
/// the normal Update pump.
///
/// It holds the REQUEST only. The pump that acts on it is
/// <see cref="SaveCutSeam"/>, because the host's deliberate return is now itself a
/// mid-run cut taken at the same frame-end seam: the cut and the leave are one
/// operation, and a cut the transient policy defers makes the leave wait a frame.
///
/// The cut it persists is the CUO world archive's (decision 164): the native
/// <c>SaveSystem.SaveGame()</c> that used to run here is gone, because decision
/// 165 makes CUO independent of <c>save.sv</c> and two writers would be two
/// sources of truth. The trigger itself stays — "the host deliberately leaves
/// the world" is the one cut the player asks for explicitly.
/// </summary>
internal sealed class RunMenuReturnCoordinator(ILogger log)
{
	private readonly RunMenuReturnRequest _request = new();
	private readonly ILogger _log = log;

	internal bool IsPending => _request.IsPending;

	/// <summary>The requested mode (a peek — the request is only cleared when the leave actually happens, or when a new session makes it stale).</summary>
	internal RunMenuReturnMode Pending => _request.Pending;

	/// <summary>Record the intent from a session-teardown event (never load a scene here).</summary>
	internal void Request(SessionRole role, bool inWorld)
	{
		var mode = RunMenuReturnPolicy.Decide(role, inWorld);
		if (mode == RunMenuReturnMode.None)
		{
			return;
		}

		_request.Request(mode);
		if (mode == RunMenuReturnMode.SaveAndMenu)
		{
			_log.LogInformation("Session ended while the host was in the world — saving the run into the CUO world archive and returning to main menu on the next pump.");
		}
		else
		{
			_log.LogInformation("Session ended while in the world — returning to main menu on the next pump.");
		}
	}

	/// <summary>The request is done with (the world was left, or a new session superseded it).</summary>
	internal void Clear() => _request.Clear();

	/// <summary>
	/// Leave the world for the main menu. The caller (<see cref="SaveCutSeam"/>)
	/// has already taken the cut — or decided there is nothing to cut — and checked
	/// that the camera exists.
	/// </summary>
	internal void Leave()
	{
		var camera = PlayerCamera.main;
		if (camera == null) // Unity object — ==
		{
			_log.LogWarning("The world was left without a scene load: the player camera is gone.");
			return;
		}

		_log.LogInformation("Leaving the world to the main menu.");
		camera.ToMainMenu();
	}
}
