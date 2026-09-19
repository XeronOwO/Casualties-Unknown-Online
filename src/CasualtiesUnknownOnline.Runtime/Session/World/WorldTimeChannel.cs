using System;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The world-time message plumbing (host authority, star shaped): a guest
/// reports the speed its own client has already applied, the host broadcasts
/// the authoritative speed down. Owns no policy — the Game Adapter's
/// WorldTimeSync owns the accept-first arbitration, the local-initiation
/// reconciliation and the all-unconscious sleep decisions; this only moves
/// frames.
/// </summary>
public sealed class WorldTimeChannel(ISessionControl session, PacketSender sender) : IWorldTimeControl
{
	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;

	/// <summary>Host: a guest applied a speed locally and reported it.</summary>
	public event Action<ulong, WorldTimeSpeed>? RequestReceived;

	/// <summary>Guest: the host broadcast the authoritative speed (a change, the resend, or the answer to this client's request).</summary>
	public event Action<WorldTimeSpeed>? TimeReceived;

	/// <summary>Guest only: report a locally applied speed to the host.</summary>
	public void SendRequest(WorldTimeSpeed speed)
	{
		if (_session.Role != SessionRole.Guest || !_session.SessionActive)
		{
			return;
		}

		_sender.Send(_session.HostSteamId, NetMsg.WorldTimeRequest, new WorldTimeRequestMsg { Speed = speed });
	}

	/// <summary>Host only: broadcast the authoritative world-time speed to every synced member.</summary>
	public void Broadcast(WorldTimeSpeed speed)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		_session.Broadcast(NetMsg.WorldTime, new WorldTimeMsg { Speed = speed });
	}

	public void FireRequestReceived(ulong sender, WorldTimeSpeed speed) => RequestReceived?.Invoke(sender, speed);

	public void FireTimeReceived(WorldTimeSpeed speed) => TimeReceived?.Invoke(speed);
}
