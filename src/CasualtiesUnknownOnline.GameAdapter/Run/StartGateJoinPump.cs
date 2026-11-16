using System;
using CasualtiesUnknownOnline.Runtime.Session;
using CasualtiesUnknownOnline.Runtime.Session.World;

namespace CasualtiesUnknownOnline.GameAdapter.Run;

/// <summary>
/// Host: while the start gate waits, re-sends the current <c>WorldJoin</c> once a second.
/// The entry instruction is one-shot; a peer that handshakes while the host's params are not
/// yet published misses it, and both sides then wait for the 30 s force. The send targets only
/// handshaken members still out of the world, so a member already loading is not disturbed.
/// </summary>
internal sealed class StartGateJoinPump
{
	private long _lastTick;

	internal void Update(ISessionControl session, IWorldControl world)
	{
		if (session.Role != SessionRole.Host || !session.SessionActive || !world.StartGateActive)
		{
			_lastTick = 0;
			return;
		}

		var now = Environment.TickCount;
		if (_lastTick != 0 && now - _lastTick < 1_000)
		{
			return;
		}

		_lastTick = now;
		world.SendWorldJoin(world.WorldParams?.IsTutorial ?? false);
	}
}
