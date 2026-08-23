using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// The host's earthquake began (host authority — guests suppress their own
/// independent quake timer and show the host's). Host → guest only
/// (direction-validated by PacketReceiver).
/// </summary>
[PacketHandler(NetMsg.EarthquakeStart, NetMessageDirection.HostToGuest)]
public sealed class EarthquakeStartHandler : PacketHandlerBase<EarthquakeStartMsg>
{
	protected override void Handle(ulong sender, EarthquakeStartMsg msg, HandlerContext ctx) =>
		ctx.World.FireEarthquakeStartReceived(msg.Duration, msg.NextDelay);
}
