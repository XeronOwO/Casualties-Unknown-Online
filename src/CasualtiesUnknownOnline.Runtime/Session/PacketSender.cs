using CasualtiesUnknownOnline.Runtime.Networking;
using CasualtiesUnknownOnline.Runtime.Protocol;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// The send side of the data plane: one Send primitive over the transport.
/// Everything that sends (session fan-out, entity stream, character reports,
/// packet handlers) depends on this tiny object instead of the transport —
/// and instead of the session (receive/send are independent mechanisms,
/// user architecture rule).
/// </summary>
public sealed class PacketSender(SteamTransport transport)
{
	private readonly SteamTransport _transport = transport;

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
}
