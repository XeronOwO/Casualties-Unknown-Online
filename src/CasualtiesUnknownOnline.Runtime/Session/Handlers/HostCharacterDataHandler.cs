using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// The host's own 1 Hz character snapshot (host → guest, host-driven): the
/// guest renders the host's clone inventory from it — the clone's slots show
/// what the host is carrying. Never applied to the local body (unlike the
/// reconnect restore, which travels on NetMsg.CharacterData).
/// </summary>
[PacketHandler(NetMsg.HostCharacterData, NetMessageDirection.HostToGuest)]
public sealed class HostCharacterDataHandler(ILogger<HostCharacterDataHandler> log)
	: PacketHandlerBase<CharacterDataMsg>
{
	private readonly ILogger<HostCharacterDataHandler> _log = log;

	protected override void Handle(ulong sender, CharacterDataMsg msg, HandlerContext ctx)
	{
		ctx.CharacterData.FireHostCharacterDataReceived(msg);
		_log.LogDebug("Host character snapshot received ({Items} items).", msg.Items.Count);
	}
}
