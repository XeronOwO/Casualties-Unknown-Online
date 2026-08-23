using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>Diagnostics reply — record the round-trip (per member).</summary>
[PacketHandler(NetMsg.Pong, NetMessageDirection.Bidirectional)]
public sealed class PongHandler : PacketHandlerBase<PongMsg, ISessionHandlerContext>
{
	protected override void Handle(ulong sender, PongMsg msg, ISessionHandlerContext ctx) => ctx.Session.RecordPong(sender, msg.Ticks);
}
