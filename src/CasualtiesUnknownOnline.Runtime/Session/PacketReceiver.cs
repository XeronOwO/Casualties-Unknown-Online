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
		if (!NetMessageRegistry.TryGet(msgId, out var metadata))
		{
			_log.LogWarning("Dropping {Raw} from {Sender}: unregistered message id.",
				frame[0], sender);
			return;
		}

		if (!metadata.IsValidFor(_session.Role))
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
	/// session — drop it instead of processing. Unregistered message ids are
	/// also invalid (fail closed). Internal so the test suite locks the
	/// direction table (CUO.Tests via InternalsVisibleTo).
	/// </summary>
	internal bool IsValidDirection(NetMsg msgId) =>
		NetMessageRegistry.TryGet(msgId, out var metadata) && metadata.IsValidFor(_session.Role);

	public void Dispose() => _transport.MessageReceived -= OnTransportMessage;
}
