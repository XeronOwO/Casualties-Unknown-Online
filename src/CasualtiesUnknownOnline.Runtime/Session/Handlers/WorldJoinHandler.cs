using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Host → guest: enter-the-world instruction. Carries the entry kind
/// (tutorial vs run) so the guest starts the right run immediately, even
/// before the world params arrive — the guest's generation boundary then
/// waits for the params (the adapter owns that wait).
/// </summary>
[PacketHandler(NetMsg.WorldJoin)]
public sealed class WorldJoinHandler : PacketHandlerBase<WorldJoinMsg>
{
	protected override void Handle(ulong sender, WorldJoinMsg msg, HandlerContext ctx) =>
		ctx.World.FireWorldJoinReceived(msg.IsTutorial);
}
