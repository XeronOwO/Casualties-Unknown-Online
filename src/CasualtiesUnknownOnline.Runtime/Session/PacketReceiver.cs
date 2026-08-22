using System;
using CasualtiesUnknownOnline.Runtime.Networking;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.NetworkTraffic;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// The receive side of the data plane: binds the transport, validates the
/// message direction for the current role and surfaces direction-valid frames
/// as <see cref="MessageArrived"/>. PacketDispatcher subscribes and routes;
/// receive and send are independent mechanisms (PacketSender), user
/// architecture rule. Depends on ISessionControl (role) only; the traffic
/// monitor is a one-way observability sink (never blocks or changes routing).
/// </summary>
public sealed class PacketReceiver : IDisposable
{
	private readonly INetworkTransport _transport;
	private readonly ISessionControl _session;
	private readonly NetworkTrafficMonitor _traffic;
	private readonly ILogger<PacketReceiver> _log;

	public PacketReceiver(INetworkTransport transport, ISessionControl session, NetworkTrafficMonitor traffic, ILogger<PacketReceiver> log)
	{
		_transport = transport;
		_session = session;
		_traffic = traffic;
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
		_traffic.RecordReceive(sender, msgId, frame.Length);
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
			or NetMsg.ItemContainerContent or NetMsg.ModCommandRequest or NetMsg.WorldTimeRequest
			or NetMsg.PlayerInventoryTakeRequest
			or NetMsg.PlayerCarryStartRequest or NetMsg.PlayerCarryStopRequest
			or NetMsg.PlayerHealRequest
			=> _session.Role == SessionRole.Host,
		NetMsg.HandshakeAck or NetMsg.WorldStartParams or NetMsg.WorldJoin or NetMsg.WorldReady
			or NetMsg.PlayerJoin or NetMsg.PlayerLeave or NetMsg.PlayerState or NetMsg.WorldBlockState
			or NetMsg.ItemReject or NetMsg.ItemSnapshot or NetMsg.HostCharacterData or NetMsg.EarthquakeStart
			or NetMsg.ItemMove or NetMsg.KeypadCode or NetMsg.TrapStateSnapshot or NetMsg.GeyserStateSnapshot
			or NetMsg.FluidRegion or NetMsg.TraderState
			or NetMsg.ItemCorrection or NetMsg.WorldItemsSnapshot or NetMsg.ItemCarriedSync
			or NetMsg.OpenedEntitiesSnapshot or NetMsg.TrapLayoutSnapshot
			or NetMsg.BuildingEntityHealthSnapshot or NetMsg.BlockDamageSnapshot
			or NetMsg.EnemyState or NetMsg.EnemySnapshot or NetMsg.EnemyAttack
			or NetMsg.ModCommandResult or NetMsg.WorldTime or NetMsg.ItemCook
			or NetMsg.FluidPresentation or NetMsg.PlayerInventoryTransfer
			or NetMsg.PlayerCarryState
			or NetMsg.PlayerHealResult
			or NetMsg.TutorialClawState
			=> _session.Role == SessionRole.Guest,
		// ModCommandRequest is guest→host only; ModCommandResult is host→guest only.
		// Ping/Pong/SceneState/BlockDamaged/CharacterData/ItemSpawn/ItemPickup/
		// ItemDrop/ItemDestroy/ItemIdWatermark/EntityEvent/EntitySpawned/
		// FluidInteraction/BlockPlaced/BuildingEntityDamaged/BuildingEntityOpened/
		// SpeechMsg: bidirectional — report up (guest → host) and broadcast down
		// (host → guest) share one message id.
		_ => true,
	};

	public void Dispose() => _transport.MessageReceived -= OnTransportMessage;
}
