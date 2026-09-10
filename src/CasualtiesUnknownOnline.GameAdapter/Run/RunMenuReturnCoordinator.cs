using System;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.Persistence;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.GameAdapter.Run;

/// <summary>
/// Owns the deferred post-session menu return for <see cref="RunCoordinator"/>.
/// Session teardown events run inside Steam/UI callbacks, so the scene load is
/// recorded here and performed on the normal Update pump. The host is the only
/// save authority, so only a host leaving a live world persists the run.
///
/// The cut it persists is the CUO world archive's (decision 164): the native
/// <c>SaveSystem.SaveGame()</c> that used to run here is gone, because decision
/// 165 makes CUO independent of <c>save.sv</c> and two writers would be two
/// sources of truth. The trigger itself stays — "the host deliberately leaves
/// the world" is the one cut the player asks for explicitly.
/// </summary>
internal sealed class RunMenuReturnCoordinator(
	ISessionControl session,
	IWorldSaveControl saves,
	ILogger log)
{
	private readonly RunMenuReturnRequest _request = new();
	private readonly ILogger _log = log;

	internal bool IsPending => _request.IsPending;

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

	/// <summary>Consume the pending request on the Update pump. A new active session cancels it as stale.</summary>
	internal void Flush(bool inWorld, Func<CharacterDataMsg?> hostCharacter)
	{
		if (!_request.TryConsume(out var mode))
		{
			return;
		}

		if (!inWorld || session.SessionActive || PlayerCamera.main == null) // Unity object — ==
		{
			return;
		}

		if (mode == RunMenuReturnMode.SaveAndMenu)
		{
			try
			{
				if (saves.TryCaptureMenuReturnCut(hostCharacter()))
				{
					_log.LogInformation("Host run persisted into the CUO world archive before leaving the world.");
				}
			}
			catch (Exception ex)
			{
				_log.LogWarning(ex, "Failed to persist the host run before leaving the world — returning to the menu anyway.");
			}
		}

		_log.LogInformation("Leaving the world to the main menu.");
		PlayerCamera.main.ToMainMenu();
	}
}
