using System;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The speech channel: player bubble reports (guest → host) and the host's
/// fan-out. A player's bubble relays to the other members (the source excluded
/// — its own bubble is local); a trader's bubble is host-broadcast to every
/// member (the host's trader is authoritative — the guests' traders are
/// suppressed from talking on their own and only replay). The bubble text is
/// the FINAL string, never re-derived on the receiving side.
/// </summary>
public sealed class SpeechChannel(ISessionControl session, PacketSender sender)
{
	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;

	/// <summary>Guest: report a locally-spoken player bubble to the host.</summary>
	public void SendSpeech(SpeechMsg msg)
	{
		if (!_session.SessionActive)
		{
			return;
		}

		_sender.Send(_session.HostSteamId, NetMsg.SpeechMsg, msg);
	}

	/// <summary>Host only: fan out a bubble (0 = every member — a trader bubble;
	/// else the source excluded — a player bubble).</summary>
	public void BroadcastSpeech(ulong excludeSteamId, SpeechMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		if (excludeSteamId == 0)
		{
			_session.Broadcast(NetMsg.SpeechMsg, msg);
		}
		else
		{
			_session.BroadcastExcept(excludeSteamId, NetMsg.SpeechMsg, msg);
		}
	}

	/// <summary>A bubble arrived: a player's report on the host, a relay on the guests.</summary>
	public event Action<ulong, SpeechMsg>? SpeechReceived;

	public void FireSpeechReceived(ulong sender, SpeechMsg msg) => SpeechReceived?.Invoke(sender, msg);
}
