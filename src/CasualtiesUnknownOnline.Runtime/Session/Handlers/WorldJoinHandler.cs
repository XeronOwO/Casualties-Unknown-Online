using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Host → guest: enter-the-world instruction. The host sent the world params
/// first (ordered, reliable), so the guest's run-start gate passes — the
/// adapter starts the run on WorldJoinReceived.
/// </summary>
[PacketHandler(NetMsg.WorldJoin)]
public sealed class WorldJoinHandler : PacketHandlerBase<WorldJoinMsg>
{
	protected override void Handle(ulong sender, WorldJoinMsg msg, HandlerContext ctx) =>
		ctx.World.FireWorldJoinReceived();
}
