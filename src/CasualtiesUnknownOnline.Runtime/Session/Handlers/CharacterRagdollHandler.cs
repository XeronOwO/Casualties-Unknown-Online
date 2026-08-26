using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// A player-character ragdoll-toggle one-shot, star semantics (local compute →
/// report → apply → fan-out): the host fires the received event so the Game
/// Adapter replays the lying pose on the owner's clone, then relays to the
/// other members (the source excluded — it already collapsed locally). Guest:
/// the host's relay — fire the event for the replay.
/// </summary>
[PacketHandler(NetMsg.CharacterRagdoll, NetMessageDirection.Bidirectional)]
public sealed class CharacterRagdollHandler(ILogger<CharacterRagdollHandler> log) : PacketHandlerBase<CharacterRagdollMsg, ICharacterSessionHandlerContext>
{
	private readonly ILogger<CharacterRagdollHandler> _log = log;

	protected override void Handle(ulong sender, CharacterRagdollMsg msg, ICharacterSessionHandlerContext ctx)
	{
		ctx.CharacterData.FireCharacterRagdollReceived(sender, msg);
		if (ctx.Session.Role == SessionRole.Host)
		{
			ctx.Session.BroadcastExcept(sender, NetMsg.CharacterRagdoll, msg);
		}

		_log.LogDebug("[CharacterRagdoll] owner {Owner} from {Sender}.", msg.OwnerSteamId, sender);
	}
}
