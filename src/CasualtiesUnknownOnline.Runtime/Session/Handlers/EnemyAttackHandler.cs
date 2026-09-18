using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Host → guest: the host's enemy simulation announced an attack to this member.
/// The guest's Game Adapter judges it against its own view — its own body and the
/// frozen enemy copy it renders — and reports the post-attack terminal state
/// through the attack-specific event when it connects. Reliable one-shot — the
/// direction table already rejects it on a host, and the star topology makes the
/// host the only possible sender (guests never address each other), so the handler
/// keeps the accept-first pattern of its neighbours and checks the local role only.
/// </summary>
[PacketHandler(NetMsg.EnemyAttack, NetMessageDirection.HostToGuest)]
public sealed class EnemyAttackHandler(ILogger<EnemyAttackHandler> log) : PacketHandlerBase<EnemyAttackMsg, IEnemySessionHandlerContext>
{
	private readonly ILogger<EnemyAttackHandler> _log = log;

	protected override void Handle(ulong sender, EnemyAttackMsg msg, IEnemySessionHandlerContext ctx)
	{
		if (ctx.Session.Role != SessionRole.Guest)
		{
			return;
		}

		ctx.Enemies.FireEnemyAttackReceived(msg);
		_log.LogInformation("[EnemyAttack] announced {Kind} #{Seq} of enemy {Enemy} from {Sender}.",
			msg.Kind, msg.AttackSeq, msg.EnemyId.ToNetworkEntityId(), sender);
	}
}
