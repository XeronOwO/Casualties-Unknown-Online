using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>Host → guest: handshake completion. Upserts the host member (a repeated
/// ack must not rebuild the entity — that would reset the interpolation buffer).
/// Answers with the AckAck leg (HandshakeAckAckMsg): the host only counts the
/// member as handshaken once it receives it (HandshakeAckAckHandler).</summary>
[PacketHandler(NetMsg.HandshakeAck, NetMessageDirection.HostToGuest)]
public sealed class HandshakeAckHandler(PacketSender sender, ILogger<HandshakeAckHandler> log) : PacketHandlerBase<HandshakeAckMsg>
{
	private readonly PacketSender _sender = sender;
	private readonly ILogger<HandshakeAckHandler> _log = log;

	protected override void Handle(ulong sender, HandshakeAckMsg msg, HandlerContext ctx)
	{
		var session = ctx.Session;
		var protocol = msg.Protocol;
		var hostState = (SceneStateType)msg.Scene.State;
		if (protocol != ProtocolVersion.Current)
		{
			_log.LogWarning("Host {Host} speaks protocol {HostProtocol}; we speak {Current}. Ending session.",
				sender, protocol, ProtocolVersion.Current);
			session.EndSession();
			return;
		}

		var member = session.GetOrCreateMember(sender);
		member.InWorld = hostState == SceneStateType.InWorld;
		member.Handshaken = true;
		session.FireMemberAdded(sender); // the handshake completed — domains keyed on member readiness hook here (the item domain grants the id watermark on the host)

		var wasActive = session.SessionActive;
		session.SessionActive = true;
		if (!wasActive)
		{
			_log.LogInformation("Handshake complete with host {Host}.", sender);
			session.FireSessionActivated();
		}

		// The ack carries the host's scene state — surface it like a regular
		// scene change so a reconnecting guest follows the host into a world
		// that is already running (Game Adapter auto-starts the run).
		session.FireRemoteSceneChanged(sender, hostState == SceneStateType.InWorld);

		// Third leg of the handshake: the host marks us Handshaken only on this
		// arrival — the start gate's wait list then holds only members whose
		// connection actually completed (a lost ack keeps a guest retrying
		// forever; without this leg the host would wait 30 s at its gate for a
		// member that is not even loading).
		_sender.Send(sender, NetMsg.HandshakeAckAck, new HandshakeAckAckMsg());
	}
}
