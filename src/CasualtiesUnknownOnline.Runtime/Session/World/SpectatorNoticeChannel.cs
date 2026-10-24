using System;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The spectator-notice channel: the watcher reports a start/stop to the host, and the host relays the same payload
/// to the watched member ONLY. It mirrors <see cref="ChatChannel"/> with one deliberate difference — the relay is
/// TARGETED (the ruling says the watched player is told, not the whole session).
/// </summary>
public sealed class SpectatorNoticeChannel(ISessionControl session, PacketSender sender)
{
	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;

	/// <summary>Watcher: report the start or the end of watching. A guest reports to the host; the HOST is itself the
	/// relay, so its own notice goes straight to the target by the same payload (there is nobody to report to).</summary>
	public void SendNotice(SpectatorNoticeMsg msg)
	{
		if (!_session.SessionActive)
		{
			return;
		}

		if (_session.Role == SessionRole.Host)
		{
			RelayToTarget(msg);
			return;
		}

		if (_session.Role != SessionRole.Guest)
		{
			return;
		}

		_sender.Send(_session.HostSteamId, NetMsg.SpectatorNotice, msg);
	}

	/// <summary>Host only: relay the notice to the watched member alone.</summary>
	public void RelayToTarget(SpectatorNoticeMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive || msg.TargetSteamId == 0)
		{
			return;
		}

		_sender.Send(msg.TargetSteamId, NetMsg.SpectatorNotice, msg);
	}

	/// <summary>A notice arrived: the watcher's report on the host, the host's relay on the watched member.</summary>
	public event Action<ulong, SpectatorNoticeMsg>? NoticeReceived;

	public void FireNoticeReceived(ulong sender, SpectatorNoticeMsg msg) => NoticeReceived?.Invoke(sender, msg);
}
