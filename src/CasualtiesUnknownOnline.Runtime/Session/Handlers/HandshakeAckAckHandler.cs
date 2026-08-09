using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Guest → host: end-to-end handshake confirmation (the ack reached the
/// guest). Only now is the member Handshaken — "the host received a handshake"
/// (HandshakeHandler) and "the guest received the ack" are different facts,
/// and the start gate's Handshaken filter only means anything when the flag
/// means "the handshake protocol completed" (see HandshakeAckAckMsg).
/// </summary>
[PacketHandler(NetMsg.HandshakeAckAck)]
public sealed class HandshakeAckAckHandler(ILogger<HandshakeAckAckHandler> log) : PacketHandlerBase<HandshakeAckAckMsg>
{
	private readonly ILogger<HandshakeAckAckHandler> _log = log;

	protected override void Handle(ulong sender, HandshakeAckAckMsg msg, HandlerContext ctx)
	{
		var session = ctx.Session;
		if (session.Role != SessionRole.Host)
		{
			return;
		}

		// Unknown sender: we never acked it (no handshake ever arrived) — a
		// stale frame from an ended session; ignore instead of fabricating a
		// member (EndSession clears the table, lobby re-joins handshake anew).
		if (!session.TryGetMember(sender, out var member))
		{
			return;
		}

		member.Handshaken = true;
		ctx.Entities.MaybeStartEntitySync(); // a confirmed member may be ready for its entity stream
		_log.LogInformation("Handshake confirmed end-to-end with {Peer}.", sender);
	}
}
