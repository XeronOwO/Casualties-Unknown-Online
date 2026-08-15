using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// A crystal lunge hit a player, star semantics (local compute → report →
/// apply → fan-out): the host applies the post-lunge limb/body state to its
/// record of the victim and relays to the other members (the source excluded —
/// it already applied locally). Guest: the host's relay — apply it.
/// Accept-first: the host adopts the victim's post-lunge state unconditionally;
/// the 1 Hz character snapshot stays the fallback.
/// </summary>
[PacketHandler(NetMsg.EnemyLunge)]
public sealed class EnemyLungeHandler(ILogger<EnemyLungeHandler> log) : PacketHandlerBase<EnemyLungeMsg>
{
	private readonly ILogger<EnemyLungeHandler> _log = log;

	protected override void Handle(ulong sender, EnemyLungeMsg msg, HandlerContext ctx)
	{
		if (ctx.Session.Role == SessionRole.Host)
		{
			ctx.CharacterData.ApplyEnemyLunge(msg);
		}

		ctx.Enemies.FireEnemyLungeReceived(sender, msg);
		if (ctx.Session.Role == SessionRole.Host)
		{
			ctx.Session.BroadcastExcept(sender, NetMsg.EnemyLunge, msg);
		}

		_log.LogInformation("[EnemyLunge] victim {Victim} limb {Limb} from {Sender}.",
			msg.VictimSteamId, msg.Limb.Index, sender);
	}
}
