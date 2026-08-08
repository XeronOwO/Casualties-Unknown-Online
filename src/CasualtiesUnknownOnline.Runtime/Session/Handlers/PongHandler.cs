using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>Diagnostics reply — record the round-trip (per member).</summary>
[PacketHandler(NetMsg.Pong)]
public sealed class PongHandler(SessionService session) : PacketHandlerBase<PongMsg>(session)
{
	protected override void Handle(ulong sender, PongMsg msg) => Session.RecordPong(sender, msg.Ticks);
}
