using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>Guest → host: protocol negotiation + member creation (new join or reconnect).</summary>
[PacketHandler(NetMsg.Handshake)]
public sealed class HandshakeHandler(PacketSender sender, ILogger<HandshakeHandler> log) : PacketHandlerBase<HandshakeMsg>
{
	private readonly PacketSender _sender = sender;
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
		_sender.Send(sender, NetMsg.HandshakeAck, new HandshakeAckMsg
		{
			Protocol = ProtocolVersion.Current,
			Scene = new SceneStateMsg { State = (byte)session.LocalSceneState },
			HasWorldParams = ctx.World.WorldParams is not null,
		});
		var worldParams = ctx.World.WorldParams;
		if (worldParams is not null)
		{
			// Params go whenever they exist: a member joining mid-generation
			// needs them the moment the host's world-entry re-invite arrives.
			_sender.Send(sender, NetMsg.WorldStartParams, worldParams.ToWorldStartParamsMsg());
			// The explicit enter instruction ONLY when the host is in a world
			// right now (order matters: params first, then the join — the
			// guest's run-start gate passes once the params are in hand; the
			// host owns the timing). Host in the menu (it captured the params
			// for an earlier run, or is still generating): no join — the guest
			// would enter a world whose host is not there and wait for a start
			// gate that never arms.
			if (session.LocalSceneState == SceneStateType.InWorld)
			{
				_sender.Send(sender, NetMsg.WorldJoin, new WorldJoinMsg { IsTutorial = worldParams.IsTutorial });
			}
		}
	}
}
