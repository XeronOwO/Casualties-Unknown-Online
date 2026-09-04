using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// A co-op location ping, star semantics (local compute → report → apply →
/// fan-out): the host fires the received event so its own client adds the
/// remote ping, then relays to the other members (the source excluded — it
/// already added the marker locally). Guest: the host's relay — fire the event
/// for the replay. Ping = transient UI presentation, no persistent state.
/// </summary>
[PacketHandler(NetMsg.LocationPing, NetMessageDirection.Bidirectional)]
public sealed class LocationPingHandler(ILogger<LocationPingHandler> log) : PacketHandlerBase<LocationPingMsg, IWorldSessionHandlerContext>
{
	private readonly ILogger<LocationPingHandler> _log = log;

	protected override void Handle(ulong sender, LocationPingMsg msg, IWorldSessionHandlerContext ctx)
	{
		ctx.World.FireLocationPingReceived(sender, msg);
		if (ctx.Session.Role == SessionRole.Host)
		{
			ctx.Session.BroadcastExcept(sender, NetMsg.LocationPing, msg);
		}

		_log.LogDebug("[LocationPing] {Kind} sender={Sender} owner={Owner} at ({X:0.0},{Y:0.0}).",
			msg.Kind, sender, msg.SenderSteamId, msg.Position.X, msg.Position.Y);
	}
}
