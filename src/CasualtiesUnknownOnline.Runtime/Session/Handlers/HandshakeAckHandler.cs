using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>Host → guest: handshake completion. Upserts the host member (a repeated
/// ack must not rebuild the entity — that would reset the interpolation buffer).</summary>
[PacketHandler(NetMsg.HandshakeAck)]
public sealed class HandshakeAckHandler(ILogger<HandshakeAckHandler> log) : PacketHandlerBase<HandshakeAckMsg>
{
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
	}
}
