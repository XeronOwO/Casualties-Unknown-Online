using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// A world-blood decal one-shot, star semantics (local compute → report →
/// apply → fan-out): the host fires the received event so the Game Adapter
/// replays the decal on its own world, then relays to the other members (the
/// source excluded — it already spawned the decal locally). Guest: the host's
/// relay — fire the event for the replay.
/// </summary>
[PacketHandler(NetMsg.WorldBloodSpawn, NetMessageDirection.Bidirectional)]
public sealed class WorldBloodSpawnHandler(ILogger<WorldBloodSpawnHandler> log) : PacketHandlerBase<WorldBloodSpawnMsg, IWorldSessionHandlerContext>
{
	private readonly ILogger<WorldBloodSpawnHandler> _log = log;

	protected override void Handle(ulong sender, WorldBloodSpawnMsg msg, IWorldSessionHandlerContext ctx)
	{
		ctx.World.FireWorldBloodSpawnReceived(sender, msg);
		if (ctx.Session.Role == SessionRole.Host)
		{
			ctx.Session.BroadcastExcept(sender, NetMsg.WorldBloodSpawn, msg);
		}

		_log.LogDebug("[WorldBloodSpawn] ({X:0.0},{Y:0.0}) ground={Ground} from {Sender}.",
			msg.Position.X, msg.Position.Y, msg.Ground, sender);
	}
}
