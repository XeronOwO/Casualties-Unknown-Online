using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Live player presentation color update. This is a cosmetic rail: a guest's
/// own color change is reported to the host, stored in the roster presence and
/// relayed to the other guests; a host color change is broadcast directly.
/// Unlike handshake/join colors, this path keeps an already-running session's
/// name tags in sync without a reconnect.
/// </summary>
[PacketHandler(NetMsg.PlayerColorUpdate, NetMessageDirection.Bidirectional)]
public sealed class PlayerColorUpdateHandler(ILogger<PlayerColorUpdateHandler> log) : PacketHandlerBase<PlayerColorUpdateMsg, ISessionHandlerContext>
{
	private readonly ILogger<PlayerColorUpdateHandler> _log = log;

	protected override void Handle(ulong sender, PlayerColorUpdateMsg msg, ISessionHandlerContext ctx)
	{
		var session = ctx.Session;
		var memberId = msg.SteamId == 0 ? sender : msg.SteamId;
		var color = msg.HasColor ? msg.Color.ToNetColorRgba() : (NetColorRgba?)null;

		if (session.Role == SessionRole.Host)
		{
			if (memberId != sender)
			{
				_log.LogWarning("Player color update from {Sender} ignored: it names another member {Member}.", sender, memberId);
				return;
			}

			session.GetOrCreateMember(sender).SelectedColor = color;
			session.BroadcastExcept(sender, NetMsg.PlayerColorUpdate, new PlayerColorUpdateMsg
			{
				SteamId = sender,
				HasColor = msg.HasColor,
				Color = msg.Color,
			});
			_log.LogInformation("Player color updated for {Member} (selected: {Selected}).", sender, msg.HasColor);
			return;
		}

		if (session.Role == SessionRole.Guest && sender == session.HostSteamId && memberId == sender)
		{
			session.GetOrCreateMember(sender).SelectedColor = color;
			_log.LogInformation("Host player color updated (selected: {Selected}).", msg.HasColor);
		}
	}
}
