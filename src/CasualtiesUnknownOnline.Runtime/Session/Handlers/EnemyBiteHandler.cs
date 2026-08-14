using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// An enemy bit a player, star semantics (local compute → report → apply →
/// fan-out): the host applies the post-bite limb/body state to its record of the
/// victim and relays to the other members (the source excluded — it already
/// applied locally). Guest: the host's relay — apply it. Accept-first: the host
/// adopts the victim's post-bite state unconditionally (no distance/legitimacy
/// validation); the 1 Hz character snapshot stays the fallback.
/// </summary>
[PacketHandler(NetMsg.EnemyBite)]
public sealed class EnemyBiteHandler(ILogger<EnemyBiteHandler> log) : PacketHandlerBase<EnemyBiteMsg>
{
	private readonly ILogger<EnemyBiteHandler> _log = log;

	protected override void Handle(ulong sender, EnemyBiteMsg msg, HandlerContext ctx)
	{
		ctx.Enemies.FireEnemyBiteReceived(sender, msg);
		if (ctx.Session.Role == SessionRole.Host)
		{
			ctx.Session.BroadcastExcept(sender, NetMsg.EnemyBite, msg);
		}

		_log.LogInformation("[EnemyBite] victim {Victim} limb {Limb} from {Sender}.", msg.VictimSteamId, msg.Limb.Index, sender);
	}
}
