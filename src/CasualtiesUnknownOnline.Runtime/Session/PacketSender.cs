using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Networking;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Session.NetworkTraffic;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// The send side of the data plane: one Send primitive over the transport.
/// Everything that sends (session fan-out, entity stream, character reports,
/// packet handlers) depends on this tiny object instead of the transport —
/// and instead of the session (receive/send are independent mechanisms,
/// user architecture rule).
/// </summary>
public sealed class PacketSender(INetworkTransport transport, NetworkTrafficMonitor traffic)
{
	private readonly INetworkTransport _transport = transport;
	private readonly NetworkTrafficMonitor _traffic = traffic;

	/// <summary>
	/// Send a message. Reliable by default — only the 20 Hz state stream
	/// (PlayerState/PlayerStateReport) goes unreliable, where overwrite
	/// semantics + snapshot sequence make drops harmless and avoid head-of-line
	/// blocking of the newest snapshot behind retransmissions.
	/// </summary>
	public void Send(ulong steamId, NetMsg msg, object? payload = null, bool reliable = true)
		=> TrySend(steamId, msg, payload, reliable);

	/// <summary>
	/// Send like <see cref="Send"/>, but report the transport's verdict
	/// (false = the frame never reached the network — peer gone, link down,
	/// Steam P2P session failed). The host's peer warm-up pump uses this to
	/// back off instead of hammering an unreachable peer every interval.
	/// </summary>
	public bool TrySend(ulong steamId, NetMsg msg, object? payload = null, bool reliable = true)
	{
		if (steamId == 0)
		{
			return false;
		}

		var frame = NetPacket.Encode(msg, payload);
		var success = _transport.SendTo(steamId, frame, reliable);
		_traffic.RecordSend(steamId, msg, frame.Length, success);
		return success;
	}

	/// <summary>
	/// Encode ONCE and send the same frame to every peer (the fan-out path —
	/// the 10 Hz item stream and the 20 Hz state stream used to pay N
	/// serializations for N recipients; the encode cost is linear in the
	/// payload, so a 100-item move batch with three guests serialized three
	/// times). The shared byte[] is safe to reuse: SteamTransport.SendTo copies
	/// it synchronously (fixed + SendMessageToUser call).
	/// </summary>
	public void SendToAll(IEnumerable<ulong> steamIds, NetMsg msg, object? payload, bool reliable = true, ulong? excludeSteamId = null)
	{
		var frame = NetPacket.Encode(msg, payload);
		foreach (var steamId in steamIds)
		{
			if (steamId != 0 && steamId != excludeSteamId)
			{
				var success = _transport.SendTo(steamId, frame, reliable);
				_traffic.RecordSend(steamId, msg, frame.Length, success);
			}
		}
	}
}
