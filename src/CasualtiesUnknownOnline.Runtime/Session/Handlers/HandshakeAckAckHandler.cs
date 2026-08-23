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
[PacketHandler(NetMsg.HandshakeAckAck, NetMessageDirection.GuestToHost)]
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

		// Fire only on the handshake→confirmed EDGE: a retried handshake cycle
		// (lazy P2P sessions swallow frames, the guest retries, the host acks
		// every repeat) re-delivers the AckAck — the member is already
		// Handshaken, and re-firing MemberAdded would duplicate every
		// readiness subscriber (the item domain's id-watermark grant, the
		// Mod API's PlayerJoined). The edge is "the member first became
		// handshaken", exactly once.
		var wasHandshaken = member.Handshaken;
		member.Handshaken = true;
		if (!wasHandshaken)
		{
			session.FireMemberAdded(sender); // the item domain grants the id watermark on this (reconnects included)
		}

		ctx.Entities.MaybeStartEntitySync(); // a confirmed member may be ready for its entity stream
		_log.LogInformation("Handshake confirmed end-to-end with {Peer}.", sender);
	}
}
