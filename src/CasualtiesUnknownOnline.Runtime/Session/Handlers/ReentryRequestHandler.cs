using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Guest → host: a member is out of the world after the host's own world exit and asks to be
/// taken back in. The request is the member's intent and nothing else — the host judges it
/// against its own presence table and answers with the ordinary targeted <c>WorldJoin</c> when it
/// holds the member out of the world. Admitted only from a handshaken member; a request from a
/// member this host holds IN the world is refused by name (its scene re-report is the settling
/// path), and what the request then means for the live world is the world domain's decision.
/// </summary>
[PacketHandler(NetMsg.ReentryRequest, NetMessageDirection.GuestToHost)]
public sealed class ReentryRequestHandler : PacketHandlerBase<ReentryRequestMsg, IReentryHandlerContext>
{
	protected override void Handle(ulong sender, ReentryRequestMsg msg, IReentryHandlerContext ctx) =>
		ctx.Reentry.HandleReentryRequest(sender);
}
