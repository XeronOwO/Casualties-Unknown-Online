using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Host → guest: the authoritative radiation-line state (host authority — the
/// host owns the line's active/timeGone world state; guests apply it).
/// </summary>
[PacketHandler(NetMsg.RadiationLineState, NetMessageDirection.HostToGuest)]
public sealed class RadiationLineStateHandler : PacketHandlerBase<RadiationLineStateMsg, IWorldHandlerContext>
{
	protected override void Handle(ulong sender, RadiationLineStateMsg msg, IWorldHandlerContext ctx) =>
		ctx.World.FireRadiationLineStateReceived(msg);
}
