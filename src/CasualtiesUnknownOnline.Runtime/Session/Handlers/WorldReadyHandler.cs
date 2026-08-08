using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Host → guest: the start-gate released (everyone loaded) or the host let a
/// late joiner in directly. The guest stops waiting and starts playing; if it
/// is still generating (forced gate timeout), it starts as soon as its own
/// generation finishes.
/// </summary>
[PacketHandler(NetMsg.WorldReady)]
public sealed class WorldReadyHandler : PacketHandlerBase<WorldReadyMsg>
{
	protected override void Handle(ulong sender, WorldReadyMsg msg, HandlerContext ctx) =>
		ctx.World.FireWorldReadyReceived();
}
