using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>Diagnostics probe — echo the sender's tick back as a pong.</summary>
[PacketHandler(NetMsg.Ping)]
public sealed class PingHandler(SessionService session) : PacketHandlerBase<PingMsg>(session)
{
	protected override void Handle(ulong sender, PingMsg msg) =>
		Session.Send(sender, NetMsg.Pong, new PongMsg { Ticks = msg.Ticks });
}
