using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// A player-character attack-animation one-shot, star semantics (local compute →
/// report → apply → fan-out): the host fires the received event so the Game
/// Adapter replays the visual on the owner's clone, then relays to the other
/// members (the source excluded — it already instantiated the visual locally).
/// Guest: the host's relay — fire the event for the replay.
/// </summary>
[PacketHandler(NetMsg.CharacterAttackAnim, NetMessageDirection.Bidirectional)]
public sealed class CharacterAttackAnimHandler(ILogger<CharacterAttackAnimHandler> log) : PacketHandlerBase<CharacterAttackAnimMsg, ICharacterSessionHandlerContext>
{
	private readonly ILogger<CharacterAttackAnimHandler> _log = log;

	protected override void Handle(ulong sender, CharacterAttackAnimMsg msg, ICharacterSessionHandlerContext ctx)
	{
		ctx.CharacterData.FireCharacterAttackAnimReceived(sender, msg);
		if (ctx.Session.Role == SessionRole.Host)
		{
			ctx.Session.BroadcastExcept(sender, NetMsg.CharacterAttackAnim, msg);
		}

		_log.LogDebug("[CharacterAttackAnim] owner {Owner} prefab {Prefab} from {Sender}.",
			msg.OwnerSteamId, msg.Prefab, sender);
	}
}
