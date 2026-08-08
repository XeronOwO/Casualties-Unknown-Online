using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Character data: guest → host as a 1 Hz report (host saves per SteamID,
/// session-scoped), host → guest as a reconnect restore.
/// </summary>
[PacketHandler(NetMsg.CharacterData)]
public sealed class CharacterDataHandler(SessionService session, ILogger<CharacterDataHandler> log)
	: PacketHandlerBase<CharacterDataMsg>(session)
{
	private readonly ILogger<CharacterDataHandler> _log = log;

	protected override void Handle(ulong sender, CharacterDataMsg msg)
	{
		if (Session.Role == SessionRole.Host)
		{
			Session.SaveCharacterData(sender, msg);
			_log.LogDebug("Saved character data for {Peer} ({Items} items).", sender, msg.Items.Count);
			return;
		}

		Session.FireCharacterDataReceived(msg);
	}
}
