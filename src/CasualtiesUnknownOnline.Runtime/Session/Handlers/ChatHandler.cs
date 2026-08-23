using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Chat;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// A text-chat line reported through the star network. At the host it is a
/// guest's report: the host validates the line and the sender identity, fires
/// the local chat event and relays to every other member (the author excluded —
/// its own UI already showed the line). At a guest it is the host's relay:
/// the line is surfaced locally and never re-relayed.
/// </summary>
[PacketHandler(NetMsg.Chat, NetMessageDirection.Bidirectional)]
public sealed class ChatHandler(ILogger<ChatHandler> log) : PacketHandlerBase<ChatMsg, IWorldSessionHandlerContext>
{
	private readonly ILogger<ChatHandler> _log = log;

	protected override void Handle(ulong sender, ChatMsg msg, IWorldSessionHandlerContext ctx)
	{
		if (!ChatPolicy.IsValid(msg.Text) || msg.SenderSteamId == 0)
		{
			_log.LogWarning("[Chat] dropping invalid line sender={Sender}.", msg.SenderSteamId);
			return;
		}

		// The host must never trust a payload that claims a different author than
		// the transport sender — only the host relay reassigns the transport sender.
		if (ctx.Session.Role == SessionRole.Host && msg.SenderSteamId != sender)
		{
			_log.LogWarning("[Chat] dropping spoofed line transport={Transport} claimed={Claimed}.", sender, msg.SenderSteamId);
			return;
		}

		ctx.World.FireChatReceived(sender, msg);

		if (ctx.Session.Role == SessionRole.Host)
		{
			ctx.World.BroadcastChat(msg.SenderSteamId, msg);
		}

		_log.LogInformation("[Chat] line sender={Sender} len={Length} role={Role}.", msg.SenderSteamId, msg.Text.Length, ctx.Session.Role);
	}
}
