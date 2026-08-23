using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// A speech bubble reported (star semantics): the host applies it to its own
/// clone of the speaker (the SpeechSync) and relays to the other members (the
/// source excluded, it already displays its own local bubble). Guest: the
/// host's relay — apply to the clone. Trader bubbles never arrive here as a
/// report (the host broadcasts them directly, the guests' traders are
/// suppressed from talking on their own).
/// </summary>
[PacketHandler(NetMsg.SpeechMsg, NetMessageDirection.Bidirectional)]
public sealed class SpeechHandler(ILogger<SpeechHandler> log) : PacketHandlerBase<SpeechMsg>
{
	private readonly ILogger<SpeechHandler> _log = log;

	protected override void Handle(ulong sender, SpeechMsg msg, HandlerContext ctx)
	{
		ctx.World.FireSpeechReceived(sender, msg);
		if (ctx.Session.Role == SessionRole.Host)
		{
			ctx.Session.BroadcastExcept(sender, NetMsg.SpeechMsg, msg);
		}

		_log.LogInformation("[Speech] from {Sender} speaker={Speaker} trader=({X:0.0},{Y:0.0}).",
			sender, msg.SpeakerSteamId, msg.TraderPosition?.X ?? 0, msg.TraderPosition?.Y ?? 0);
	}
}
