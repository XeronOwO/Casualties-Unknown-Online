using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>Diagnostics probe — echo the sender's tick back as a pong.</summary>
[PacketHandler(NetMsg.Ping)]
public sealed class PingHandler : PacketHandlerBase<PingMsg>
{
	protected override void Handle(ulong sender, PingMsg msg, HandlerContext ctx) =>
		ctx.Session.Send(sender, NetMsg.Pong, new PongMsg { Ticks = msg.Ticks });
}
