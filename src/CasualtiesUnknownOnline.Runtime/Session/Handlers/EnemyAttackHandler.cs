using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Host → guest: the host's enemy simulation ordered an attack on this member.
/// The victim's Game Adapter applies the attack to its local body and reports
/// the post-attack terminal state through the attack-specific event. Reliable
/// one-shot — the direction table already rejects it on a host.
/// </summary>
[PacketHandler(NetMsg.EnemyAttack)]
public sealed class EnemyAttackHandler(ILogger<EnemyAttackHandler> log) : PacketHandlerBase<EnemyAttackMsg>
{
	private readonly ILogger<EnemyAttackHandler> _log = log;

	protected override void Handle(ulong sender, EnemyAttackMsg msg, HandlerContext ctx)
	{
		if (ctx.Session.Role != SessionRole.Guest)
		{
			return;
		}

		ctx.Enemies.FireEnemyAttackReceived(msg);
		_log.LogInformation("[EnemyAttack] {Kind} on victim {Victim} enemy {Enemy} from {Sender}.",
			msg.Kind, msg.VictimSteamId, msg.EnemyId.ToNetworkEntityId(), sender);
	}
}
