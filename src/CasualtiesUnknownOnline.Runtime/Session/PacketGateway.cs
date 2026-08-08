using System;
using CasualtiesUnknownOnline.Runtime.Networking;
using CasualtiesUnknownOnline.Runtime.Protocol;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// Data-plane gateway: owns the transport binding and the wire handling
/// (frame encode/decode, direction validation). SessionService is the control
/// plane — it maintains the session and exposes business-level send/receive
/// APIs; every message it sends goes through <see cref="Send"/>. Direction-
/// valid received frames are surfaced as <see cref="MessageArrived"/> and the
/// session dispatches them through the router (the gateway does not depend on
/// the router/handlers, so the constructor graph stays acyclic — abstract
/// extraction, user rule).
/// </summary>
public sealed class PacketGateway : IDisposable
{
	private readonly SteamTransport _transport;
	private readonly SessionIdentity _identity;
	private readonly ILogger<PacketGateway> _log;

	public PacketGateway(
		SteamTransport transport, SessionIdentity identity, ILogger<PacketGateway> log)
	{
		_transport = transport;
		_identity = identity;
		_log = log;
		transport.MessageReceived += OnTransportMessage;
	}

	/// <summary>
	/// Raised for every direction-valid frame (sender SteamId + raw frame). The
	/// session subscribes and dispatches through the router.
	/// </summary>
	public event Action<ulong, byte[]>? MessageArrived;

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

	private void OnTransportMessage(ulong sender, byte[] frame)
	{
		if (frame.Length < 1)
		{
			return;
		}

		var msgId = (NetMsg)frame[0];
		if (!IsValidDirection(msgId))
		{
			_log.LogWarning("Dropping {Msg} from {Sender}: illegal direction for role {Role}.",
				msgId, sender, _identity.Role);
			return;
		}

		MessageArrived?.Invoke(sender, frame);
	}

	/// <summary>
	/// One-way messages must arrive at the role they were sent to. Anything
	/// else means a misbehaving peer or a stale message from a previous
	/// session — drop it instead of processing.
	/// </summary>
	private bool IsValidDirection(NetMsg msgId) => msgId switch
	{
		NetMsg.Handshake or NetMsg.PlayerStateReport => _identity.Role == SessionRole.Host,
		NetMsg.HandshakeAck or NetMsg.WorldStartParams or NetMsg.PlayerJoin
			or NetMsg.PlayerLeave or NetMsg.PlayerState => _identity.Role == SessionRole.Guest,
		// Ping/Pong/SceneState/BlockDamaged/CharacterData: bidirectional —
		// report up (guest → host) and broadcast down (host → guest)
		// share one message id.
		_ => true,
	};

	public void Dispose() => _transport.MessageReceived -= OnTransportMessage;
}
