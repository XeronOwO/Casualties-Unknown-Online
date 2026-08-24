using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// A player-character landing-visual one-shot, star semantics (local compute →
/// report → apply → fan-out): the host fires the received event so the Game
/// Adapter replays the Grounded clip/dust on the owner's clone, then relays to
/// the other members (the source excluded — it already saw the visual locally).
/// Guest: the host's relay — fire the event for the replay.
/// </summary>
[PacketHandler(NetMsg.CharacterLandingVisual, NetMessageDirection.Bidirectional)]
public sealed class CharacterLandingVisualHandler(ILogger<CharacterLandingVisualHandler> log) : PacketHandlerBase<CharacterLandingVisualMsg, ICharacterSessionHandlerContext>
{
	private readonly ILogger<CharacterLandingVisualHandler> _log = log;

	protected override void Handle(ulong sender, CharacterLandingVisualMsg msg, ICharacterSessionHandlerContext ctx)
	{
		ctx.CharacterData.FireCharacterLandingVisualReceived(sender, msg);
		if (ctx.Session.Role == SessionRole.Host)
		{
			ctx.Session.BroadcastExcept(sender, NetMsg.CharacterLandingVisual, msg);
		}

		_log.LogDebug("[CharacterLandingVisual] owner {Owner} cloud {Cloud} from {Sender}.",
			msg.OwnerSteamId, msg.CloudSize, sender);
	}
}
