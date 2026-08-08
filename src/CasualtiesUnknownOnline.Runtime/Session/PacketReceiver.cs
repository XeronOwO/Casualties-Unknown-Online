using System;
using CasualtiesUnknownOnline.Runtime.Networking;
using CasualtiesUnknownOnline.Runtime.Protocol;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// The receive side of the data plane: binds the transport, validates the
/// message direction for the current role and surfaces direction-valid frames
/// as <see cref="MessageArrived"/>. The session subscribes and dispatches
/// through the router (it owns the handler context); receive and send are
/// independent mechanisms (PacketSender), user architecture rule.
/// </summary>
public sealed class PacketReceiver : IDisposable
{
	private readonly SteamTransport _transport;
	private readonly SessionIdentity _identity;
	private readonly ILogger<PacketReceiver> _log;

	public PacketReceiver(SteamTransport transport, SessionIdentity identity, ILogger<PacketReceiver> log)
	{
		_transport = transport;
		_identity = identity;
		_log = log;
		transport.MessageReceived += OnTransportMessage;
	}

	/// <summary>Raised for every direction-valid frame (sender SteamId + raw frame).</summary>
	public event Action<ulong, byte[]>? MessageArrived;

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
