using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// An enemy proximity side effect fired on a player, star semantics (local
/// compute → report → apply → fan-out): the host applies the post-effect body
/// state to its record of the victim and relays to the other members (the
/// source excluded — it already applied locally). Guest: the host's relay —
/// apply it. Accept-first: the host adopts the victim's post-effect state
/// unconditionally; the 1 Hz character snapshot stays the fallback.
/// </summary>
[PacketHandler(NetMsg.EnemyEffect, NetMessageDirection.Bidirectional)]
public sealed class EnemyEffectHandler(ILogger<EnemyEffectHandler> log) : PacketHandlerBase<EnemyEffectMsg>
{
	private readonly ILogger<EnemyEffectHandler> _log = log;

	protected override void Handle(ulong sender, EnemyEffectMsg msg, HandlerContext ctx)
	{
		if (ctx.Session.Role == SessionRole.Host)
		{
			ctx.CharacterData.ApplyEnemyEffect(msg);
		}

		ctx.Enemies.FireEnemyEffectReceived(sender, msg);
		if (ctx.Session.Role == SessionRole.Host)
		{
			ctx.Session.BroadcastExcept(sender, NetMsg.EnemyEffect, msg);
		}

		_log.LogInformation("[EnemyEffect] {Kind} on victim {Victim} from {Sender}.", msg.Kind, msg.VictimSteamId, sender);
	}
}
