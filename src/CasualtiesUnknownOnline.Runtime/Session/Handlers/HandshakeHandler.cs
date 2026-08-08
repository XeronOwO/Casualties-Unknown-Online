using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>Guest → host: protocol negotiation + member creation (new join or reconnect).</summary>
[PacketHandler(NetMsg.Handshake)]
public sealed class HandshakeHandler(SessionService session, CharacterDataStore store, ILogger<HandshakeHandler> log)
	: PacketHandlerBase<HandshakeMsg>(session)
{
	private readonly CharacterDataStore _store = store;
	private readonly ILogger<HandshakeHandler> _log = log;

	protected override void Handle(ulong sender, HandshakeMsg msg)
	{
		if (Session.Role != SessionRole.Host)
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
		if (!Session.IsLobbyMember(sender))
		{
			_log.LogWarning("Handshake from {Peer} ignored: not a lobby member.", sender);
			return;
		}

		var wasActive = Session.SessionActive;
		if (!Session.TryGetMember(sender, out var member))
		{
			member = Session.GetOrCreateMember(sender);
			member.Entity.InWorld = peerState == SceneStateType.InWorld;
			// Cross-session restore: the in-memory character save outlives the
			// session (kept per SteamID for the process lifetime) — a returning
			// player gets it back even in a brand-new session.
			_store.SendSavedCharacter(sender);
		}
		else
		{
			// Reconnect from the same player while the entity is still held
			// (within the presence-check window, or a quick lobby round trip):
			// identity is the SteamID — reuse the entity. The normal flow
			// (session re-activation → scene re-report → entity sync) then
			// re-establishes everything, character data included.
			member.Entity.InWorld = peerState == SceneStateType.InWorld;
			_log.LogInformation("Peer {Peer} reconnected — entity reused.", sender);
			_store.SendSavedCharacter(sender);
		}

		member.Handshaken = true;
		if (!wasActive)
		{
			// Fire the session-level event once, on the first member — later
			// members only take the member-level path.
			Session.SessionActive = true;
			_log.LogInformation("Handshake complete with {Peer}.", sender);
			Session.FireSessionActivated();
		}

		Session.MaybeStartEntitySync();

		// Ack on every handshake, even repeats: the guest retransmits its
		// handshake until it receives one (Steam P2P sessions establish lazily,
		// first messages can be swallowed — Phase-0 finding). Same for world
		// params, which are only sent once the session exists.
		Session.Send(sender, NetMsg.HandshakeAck, new HandshakeAckMsg
		{
			Protocol = ProtocolVersion.Current,
			Scene = Session.CreateSceneStateMsg(),
			HasWorldParams = Session.WorldParams is not null,
		});
		if (Session.WorldParams is not null)
		{
			Session.Send(sender, NetMsg.WorldStartParams, WorldStartParamsMsg.From(Session.WorldParams));
		}
	}
}
