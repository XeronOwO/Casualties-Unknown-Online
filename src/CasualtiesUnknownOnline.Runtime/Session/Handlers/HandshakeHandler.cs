using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>Guest → host: protocol negotiation + member creation (new join or reconnect).</summary>
[PacketHandler(NetMsg.Handshake)]
public sealed class HandshakeHandler(ILogger<HandshakeHandler> log) : PacketHandlerBase<HandshakeMsg>
{
	private readonly ILogger<HandshakeHandler> _log = log;

	protected override void Handle(ulong sender, HandshakeMsg msg, HandlerContext ctx)
	{
		var session = ctx.Session;
		if (session.Role != SessionRole.Host)
		{
			return;
		}

		var protocol = msg.Protocol;
		var peerState = (SceneStateType)msg.Scene.State;
		if (protocol != ProtocolVersion.Current)
		{
			_log.LogWarning("Peer {Peer} speaks protocol {PeerProtocol}; we speak {Current}. Rejecting.",
				sender, protocol, ProtocolVersion.Current);
			return;
		}

		// Star network: only lobby members may join — the lobby is the roster.
		// A third player is no longer rejected, they become a new member.
		if (!session.IsLobbyMember(sender))
		{
			_log.LogWarning("Handshake from {Peer} ignored: not a lobby member.", sender);
			return;
		}

		var wasActive = session.SessionActive;
		if (!session.TryGetMember(sender, out var member))
		{
			member = session.GetOrCreateMember(sender);
			member.InWorld = peerState == SceneStateType.InWorld;
			// Cross-session restore: the in-memory character save outlives the
			// session (kept per SteamID for the process lifetime) — a returning
			// player gets it back even in a brand-new session.
			ctx.CharacterData.SendSavedCharacter(sender);
		}
		else
		{
			// Reconnect from the same player while the member is still held
			// (within the presence-check window, or a quick lobby round trip):
			// identity is the SteamID — reuse the presence. The normal flow
			// (session re-activation → scene re-report → entity sync) then
			// re-establishes everything, character data included.
			member.InWorld = peerState == SceneStateType.InWorld;
			_log.LogInformation("Peer {Peer} reconnected — presence reused.", sender);
			ctx.CharacterData.SendSavedCharacter(sender);
		}

		member.Handshaken = true;
		if (!wasActive)
		{
			// Fire the session-level event once, on the first member — later
			// members only take the member-level path.
			session.SessionActive = true;
			_log.LogInformation("Handshake complete with {Peer}.", sender);
			session.FireSessionActivated();
		}

		ctx.Entities.MaybeStartEntitySync();

		// Ack on every handshake, even repeats: the guest retransmits its
		// handshake until it receives one (Steam P2P sessions establish lazily,
		// first messages can be swallowed — Phase-0 finding). Same for world
		// params, which are only sent once the session exists.
		session.Send(sender, NetMsg.HandshakeAck, new HandshakeAckMsg
		{
			Protocol = ProtocolVersion.Current,
			Scene = new SceneStateMsg { State = (byte)session.LocalSceneState },
			HasWorldParams = session.WorldParams is not null,
		});
		var worldParams = session.WorldParams;
		if (worldParams is not null)
		{
			session.Send(sender, NetMsg.WorldStartParams, worldParams.ToWorldStartParamsMsg());
		}
	}
}
