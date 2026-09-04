using System;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The co-op location-ping channel: guest → host report and the host's fan-out
/// to the other members (source excluded). The ping is a transient one-shot
/// presentation fact; the host only relays it — it never stores it and never
/// turns it into world state.
/// </summary>
public sealed class LocationPingChannel(ISessionControl session, PacketSender sender)
{
	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;

	/// <summary>A location-ping event arrived (host: guest report; guest: host relay).</summary>
	public event Action<ulong, LocationPingMsg>? LocationPingReceived;

	public void FireLocationPingReceived(ulong sender, LocationPingMsg msg) => LocationPingReceived?.Invoke(sender, msg);

	/// <summary>
	/// Report a locally-placed location ping: a guest sends to the host; a host
	/// broadcasts to every handshaken member (it already added the marker
	/// locally). One ping = one message.
	/// </summary>
	public void SendLocationPing(LocationPingMsg msg)
	{
		if (!_session.SessionActive)
		{
			return;
		}

		if (_session.Role == SessionRole.Host)
		{
			_session.Broadcast(NetMsg.LocationPing, msg);
		}
		else
		{
			_sender.Send(_session.HostSteamId, NetMsg.LocationPing, msg);
		}
	}

	/// <summary>Host only: relay an accepted location ping to the other members.</summary>
	public void BroadcastLocationPing(ulong excludeSteamId, LocationPingMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		_session.BroadcastExcept(excludeSteamId, NetMsg.LocationPing, msg);
	}
}
