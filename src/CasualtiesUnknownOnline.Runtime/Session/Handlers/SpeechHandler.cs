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
///
/// <para>
/// The speaker key is a claim, and the bubble is replayed on the clone of the
/// member it names (<c>SpeechSync.ReplayOnClone</c>), so it is refused by the same
/// rule a chat line claiming another author already meets: on the host, an inbound
/// frame must name the member that sent it (<c>SpeechSync.OnSpeechReported</c>
/// stamps <c>_session.LocalSteamId</c>; the local id never enters the presence
/// table, so every frame this handler sees on the host came from a member). The
/// comparison is host-side only — the relay's transport sender is the host while
/// the payload names the speaker, so a guest must not judge it — and the trader
/// shape (<c>SpeakerSteamId == 0</c>) is host-only by construction, so a member
/// claiming it is refused too. What that leaves unbounded is named in the census
/// row: a forged frame addressed straight at a GUEST is replayed there as
/// presentation, which the owner's own next bubble overwrites.
/// </para>
/// </summary>
[PacketHandler(NetMsg.SpeechMsg, NetMessageDirection.Bidirectional)]
public sealed class SpeechHandler(ILogger<SpeechHandler> log) : PacketHandlerBase<SpeechMsg, IWorldSessionHandlerContext>
{
	private readonly ILogger<SpeechHandler> _log = log;

	protected override void Handle(ulong sender, SpeechMsg msg, IWorldSessionHandlerContext ctx)
	{
		if (ctx.Session.Role == SessionRole.Host && msg.SpeakerSteamId != sender)
		{
			_log.LogWarning("[Speech] dropped spoofed bubble from {Sender} claiming speaker {Speaker}.", sender, msg.SpeakerSteamId);
			return;
		}

		ctx.World.FireSpeechReceived(sender, msg);
		if (ctx.Session.Role == SessionRole.Host)
		{
			ctx.Session.BroadcastExcept(sender, NetMsg.SpeechMsg, msg);
		}

		_log.LogInformation("[Speech] from {Sender} speaker={Speaker} trader=({X:0.0},{Y:0.0}).",
			sender, msg.SpeakerSteamId, msg.TraderPosition?.X ?? 0, msg.TraderPosition?.Y ?? 0);
	}
}
