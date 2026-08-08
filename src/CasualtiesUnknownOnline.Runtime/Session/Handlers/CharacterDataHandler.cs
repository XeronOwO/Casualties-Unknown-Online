using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Character data: guest → host as a 1 Hz report (host saves per SteamID,
/// session-scoped), host → guest as a reconnect restore.
/// </summary>
[PacketHandler(NetMsg.CharacterData)]
public sealed class CharacterDataHandler(ILogger<CharacterDataHandler> log) : PacketHandlerBase<CharacterDataMsg>
{
	private readonly ILogger<CharacterDataHandler> _log = log;

	protected override void Handle(ulong sender, CharacterDataMsg msg, HandlerContext ctx)
	{
		if (ctx.Session.Role == SessionRole.Host)
		{
			ctx.CharacterData.SaveCharacterData(sender, msg);
			// Render the reporter's clone inventory (the adapter diff-applies
			// the snapshot to the remote clone's slots).
			ctx.CharacterData.FireCharacterDataReceived(sender, msg);
			_log.LogDebug("Saved character data for {Peer} ({Items} items).", sender, msg.Items.Count);
			return;
		}

		ctx.CharacterData.FireCharacterDataReceived(sender, msg);
	}
}
