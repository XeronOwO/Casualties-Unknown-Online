using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// A hostile trader swing presentation one-shot, star semantics (local compute →
/// report → apply → fan-out): the host fires the received event so the Game
/// Adapter replays the animation on its own same-position trader, then relays
/// to the other members (the source excluded — it already instantiated the
/// visual locally). Guest: the host's relay — fire the event for the replay.
/// </summary>
[PacketHandler(NetMsg.TraderSwing, NetMessageDirection.Bidirectional)]
public sealed class TraderSwingHandler(ILogger<TraderSwingHandler> log) : PacketHandlerBase<TraderSwingMsg, IWorldSessionHandlerContext>
{
	private readonly ILogger<TraderSwingHandler> _log = log;

	protected override void Handle(ulong sender, TraderSwingMsg msg, IWorldSessionHandlerContext ctx)
	{
		ctx.World.FireTraderSwingReceived(sender, msg);
		if (ctx.Session.Role == SessionRole.Host)
		{
			ctx.Session.BroadcastExcept(sender, NetMsg.TraderSwing, msg);
		}

		_log.LogDebug("[TraderSwing] trader=({X:0.0},{Y:0.0}) from {Sender}.", msg.Position.X, msg.Position.Y, sender);
	}
}
