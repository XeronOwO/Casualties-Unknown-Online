using System;
using CasualtiesUnknownOnline.Runtime.Networking;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.Handlers;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// Data-plane gateway: owns the transport binding and the wire handling
/// (frame encode/decode, direction validation, dispatch). SessionService is
/// the control plane — it maintains the session and exposes business-level
/// send/receive APIs; every message it sends goes through <see cref="Send"/>,
/// every received frame arrives here and is routed to its packet handler.
/// </summary>
public sealed class PacketGateway : IDisposable
{
	private readonly SteamTransport _transport;
	private readonly SessionService _session;
	private readonly PacketRouter _router;
	private readonly ILogger<PacketGateway> _log;

	public PacketGateway(
		SteamTransport transport, SessionService session, PacketRouter router, ILogger<PacketGateway> log)
	{
		_transport = transport;
		_session = session;
		_router = router;
		_log = log;
		transport.MessageReceived += OnMessage;
	}

	/// <summary>
	/// Send a message. Reliable by default — only the 20 Hz state stream
	/// (PlayerState/PlayerStateReport) goes unreliable, where overwrite
	/// semantics + snapshot sequence make drops harmless and avoid head-of-line
	/// blocking of the newest snapshot behind retransmissions.
	/// </summary>
	public void Send(ulong steamId, NetMsg msg, object? payload = null, bool reliable = true)
	{
		if (steamId == 0)
		{
			return;
		}

		_transport.SendTo(steamId, NetPacket.Encode(msg, payload), reliable);
	}

	private void OnMessage(ulong sender, byte[] frame)
	{
		if (frame.Length < 1)
		{
			return;
		}

		var msgId = (NetMsg)frame[0];
		if (!IsValidDirection(msgId))
		{
			_log.LogWarning("Dropping {Msg} from {Sender}: illegal direction for role {Role}.",
				msgId, sender, _session.Role);
			return;
		}

		// O(1) dictionary route to the per-message handler (Session/Handlers/).
		if (_router.TryDispatch(sender, frame))
		{
			return;
		}

		_log.LogWarning("No handler for {Msg} from {Sender}.", msgId, sender);
	}

	/// <summary>
	/// One-way messages must arrive at the role they were sent to. Anything
	/// else means a misbehaving peer or a stale message from a previous
	/// session — drop it instead of processing.
	/// </summary>
	private bool IsValidDirection(NetMsg msgId) => msgId switch
	{
		NetMsg.Handshake or NetMsg.PlayerStateReport => _session.Role == SessionRole.Host,
		NetMsg.HandshakeAck or NetMsg.WorldStartParams or NetMsg.PlayerJoin
			or NetMsg.PlayerLeave or NetMsg.PlayerState => _session.Role == SessionRole.Guest,
		// Ping/Pong/SceneState/BlockDamaged/CharacterData: bidirectional —
		// report up (guest → host) and broadcast down (host → guest)
		// share one message id.
		_ => true,
	};

	public void Dispose() => _transport.MessageReceived -= OnMessage;
}
