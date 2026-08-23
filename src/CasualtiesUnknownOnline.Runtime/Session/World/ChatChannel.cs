using System;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.World;

/// <summary>
/// The text-chat channel: guest → host reports and the host's fan-out to the
/// other members. The host is the only relay — a guest never sends a chat line
/// directly to another guest; the wire keeps one bidirectional message shape so
/// both the upward report and the downward broadcast use the same payload.
/// </summary>
public sealed class ChatChannel(ISessionControl session, PacketSender sender)
{
	private readonly ISessionControl _session = session;
	private readonly PacketSender _sender = sender;

	/// <summary>Guest: report a locally-authored chat line to the host.</summary>
	public void SendChat(ChatMsg msg)
	{
		if (!_session.SessionActive || _session.Role != SessionRole.Guest)
		{
			return;
		}

		_sender.Send(_session.HostSteamId, NetMsg.Chat, msg);
	}

	/// <summary>Host only: fan a chat line out to every member except the author.</summary>
	public void BroadcastChat(ulong excludeSteamId, ChatMsg msg)
	{
		if (_session.Role != SessionRole.Host || !_session.SessionActive)
		{
			return;
		}

		_session.BroadcastExcept(excludeSteamId, NetMsg.Chat, msg);
	}

	/// <summary>A chat line arrived: a guest report on the host, a relay on the guests.</summary>
	public event Action<ulong, ChatMsg>? ChatReceived;

	public void FireChatReceived(ulong sender, ChatMsg msg) => ChatReceived?.Invoke(sender, msg);
}
