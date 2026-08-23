using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// A player-character action sound, star semantics (local compute → report →
/// apply → fan-out): the host fires the received event so the Game Adapter
/// replays the sound on the owner's clone, then relays to the other members
/// (the source excluded — it already heard its own sound). Guest: the host's
/// relay — fire the event for the replay. One sound = one reliable message.
/// </summary>
[PacketHandler(NetMsg.CharacterSound, NetMessageDirection.Bidirectional)]
public sealed class CharacterSoundHandler(ILogger<CharacterSoundHandler> log) : PacketHandlerBase<CharacterSoundMsg, ICharacterSessionHandlerContext>
{
	private readonly ILogger<CharacterSoundHandler> _log = log;

	protected override void Handle(ulong sender, CharacterSoundMsg msg, ICharacterSessionHandlerContext ctx)
	{
		ctx.CharacterData.FireCharacterSoundReceived(sender, msg);
		if (ctx.Session.Role == SessionRole.Host)
		{
			ctx.Session.BroadcastExcept(sender, NetMsg.CharacterSound, msg);
		}

		_log.LogInformation("[CharacterSound] owner {Owner} kind {Kind} clip {Clip} from {Sender}.",
			msg.OwnerSteamId, msg.Kind, msg.Clip, sender);
	}
}
