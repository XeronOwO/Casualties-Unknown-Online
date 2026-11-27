using System;
using CasualtiesUnknownOnline.GameAdapter.Character;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.World;

namespace CasualtiesUnknownOnline.GameAdapter.Run;

/// <summary>
/// Host: while the start gate waits, re-sends the current <c>WorldJoin</c> once a second.
/// The entry instruction is one-shot; a peer that handshakes while the host's params are not
/// yet published misses it, and both sides then wait for the 30 s force. The send targets only
/// handshaken members still out of the world, so a member already loading is not disturbed.
/// Each invitation is preceded by the member's character hand-over: the instruction is the
/// edge at which the member builds its body, and a body built first replaces the restored
/// character with the starting supplies (see <see cref="CharacterDataSync.HandOverEnteringMembers"/>).
/// </summary>
internal sealed class StartGateJoinPump
{
	// The member's own world load measured 37-50 s in a sandbox client (2026-11-27: savetime-1 48.6 s); a
	// ten-second window closed before the member could answer, so every continue row paid a manual rejoin.
	private const long ContinueResendWindowMs = 60_000;

	private long _lastTick;
	private long _continueDeadline;

	/// <summary>
	/// Host: a Continue restores a run whose epoch is newer than the one the member
	/// last followed. The member can still look in-world while its scene returns to
	/// the menu, so the one-shot in-world join is skipped; keep inviting for a short
	/// window until the member reports out of the world and receives the new epoch.
	/// </summary>
	internal bool ArmAfterContinue(ISessionControl session, IWorldControl world, CharacterDataSync characterData)
	{
		if (session.Role != SessionRole.Host || !session.SessionActive)
		{
			return true;
		}

		// A continue announces a run entry: every member still outside takes its restored
		// character before the invitation below, however many times this pump repeats it.
		characterData.BeginRunEntry();
		_continueDeadline = Environment.TickCount + ContinueResendWindowMs;
		_lastTick = 0;
		world.SetHostRunPending(true);
		return true;
	}

	internal void Update(ISessionControl session, IWorldControl world, CharacterDataSync characterData)
	{
		if (session.Role != SessionRole.Host || !session.SessionActive)
		{
			_lastTick = 0;
			_continueDeadline = 0;
			return;
		}

		var now = Environment.TickCount;
		if (_continueDeadline != 0 && now - _continueDeadline >= 0)
		{
			_continueDeadline = 0;
		}

		if (!world.StartGateActive && _continueDeadline == 0)
		{
			_lastTick = 0;
			return;
		}

		if (_lastTick != 0 && now - _lastTick < 1_000)
		{
			return;
		}

		_lastTick = now;
		characterData.HandOverEnteringMembers();
		world.SendWorldJoin(world.WorldParams?.IsTutorial ?? false);
	}
}
