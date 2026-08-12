using System;
using CasualtiesUnknownOnline.Runtime.Networking;
using CasualtiesUnknownOnline.Runtime.Protocol;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// The receive side of the data plane: binds the transport, validates the
/// message direction for the current role and surfaces direction-valid frames
/// as <see cref="MessageArrived"/>. PacketDispatcher subscribes and routes;
/// receive and send are independent mechanisms (PacketSender), user
/// architecture rule. Depends on ISessionControl (role) only.
/// </summary>
public sealed class PacketReceiver : IDisposable
{
	private readonly INetworkTransport _transport;
	private readonly ISessionControl _session;
	private readonly ILogger<PacketReceiver> _log;

	public PacketReceiver(INetworkTransport transport, ISessionControl session, ILogger<PacketReceiver> log)
	{
		_transport = transport;
		_session = session;
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
				msgId, sender, _session.Role);
			return;
		}

		MessageArrived?.Invoke(sender, frame);
	}

	/// <summary>
	/// One-way messages must arrive at the role they were sent to. Anything
	/// else means a misbehaving peer or a stale message from a previous
	/// session — drop it instead of processing. Internal so the test suite
	/// locks the direction table (CUO.Tests via InternalsVisibleTo).
	/// </summary>
	internal bool IsValidDirection(NetMsg msgId) => msgId switch
	{
		NetMsg.Handshake or NetMsg.PlayerStateReport or NetMsg.HandshakeAckAck
			or NetMsg.TraderAction or NetMsg.ItemUse or NetMsg.ItemSlot or NetMsg.CarriedInventory
			=> _session.Role == SessionRole.Host,
		NetMsg.HandshakeAck or NetMsg.WorldStartParams or NetMsg.WorldJoin or NetMsg.WorldReady
			or NetMsg.PlayerJoin or NetMsg.PlayerLeave or NetMsg.PlayerState or NetMsg.WorldBlockState
			or NetMsg.ItemReject or NetMsg.ItemSnapshot or NetMsg.HostCharacterData or NetMsg.EarthquakeStart
			or NetMsg.ItemMove or NetMsg.KeypadCode or NetMsg.TrapStateSnapshot or NetMsg.GeyserStateSnapshot
			or NetMsg.FluidRegion or NetMsg.TraderState
			or NetMsg.ItemCorrection or NetMsg.WorldItemsSnapshot or NetMsg.ItemCarriedSync
			=> _session.Role == SessionRole.Guest,
		// Ping/Pong/SceneState/BlockDamaged/CharacterData/ItemSpawn/ItemPickup/
		// ItemDrop/ItemDestroy/ItemIdWatermark/EntityEvent/EntitySpawned/
		// FluidInteraction/BlockPlaced/BuildingEntityDamaged/BuildingEntityOpened:
		// bidirectional — report up (guest → host) and broadcast down (host →
		// guest) share one message id.
		_ => true,
	};

	public void Dispose() => _transport.MessageReceived -= OnTransportMessage;
}
