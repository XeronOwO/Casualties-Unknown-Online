using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Character data: guest → host as a 1 Hz report (host saves per SteamID,
/// memory + disk) relayed to the other guests (their clones of the reporter
/// render its carried state), host → guest as a reconnect restore.
/// </summary>
[PacketHandler(NetMsg.CharacterData, NetMessageDirection.Bidirectional)]
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
			// Relay to the other guests: a guest's clone of another guest
			// renders from the latest relayed snapshot — without this the
			// guests can never see each other's carried/worn state ("host sees
			// guest 1's legpouch, guest 2 sees nothing").
			ctx.CharacterData.RelayCharacterData(sender, msg);
			_log.LogDebug("Saved character data for {Peer} ({Items} items).", sender, msg.Items.Count);
			return;
		}

		ctx.CharacterData.FireCharacterDataReceived(sender, msg);
	}
}
