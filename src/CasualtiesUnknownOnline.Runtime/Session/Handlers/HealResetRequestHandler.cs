using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Guest → host: a member typed the cheat verb <c>/heal &lt;selector&gt;</c>. The host judges its
/// own cheat switch and roster, then resets its own body where it is named and instructs every
/// named member's client — the target's own body is the only place its reset can run — and answers
/// with one sentence the member's console prints.
/// </summary>
[PacketHandler(NetMsg.HealResetRequest, NetMessageDirection.GuestToHost)]
internal sealed class HealResetRequestHandler : PacketHandlerBase<HealResetRequestMsg, IHealResetHandlerContext>
{
	protected override void Handle(ulong sender, HealResetRequestMsg msg, IHealResetHandlerContext ctx) =>
		ctx.HealReset.HandleRequest(sender, msg);
}
