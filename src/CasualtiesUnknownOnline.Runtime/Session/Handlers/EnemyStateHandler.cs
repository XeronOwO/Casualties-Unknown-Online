using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>Host → guest: the authoritative enemy-state batch (unreliable, seq-gated).</summary>
[PacketHandler(NetMsg.EnemyState, NetMessageDirection.HostToGuest)]
public sealed class EnemyStateHandler : PacketHandlerBase<EnemyStateBatchMsg>
{
	protected override void Handle(ulong sender, EnemyStateBatchMsg msg, HandlerContext ctx)
	{
		if (ctx.Session.Role != SessionRole.Guest)
		{
			return;
		}

		var enemies = ctx.Enemies;
		// Unreliable stream: drop stale snapshots (reordered or duplicate) —
		// the broadcast has a single source (the host).
		if (msg.Seq <= enemies.LastEnemyStateSeq)
		{
			return;
		}

		enemies.LastEnemyStateSeq = msg.Seq;
		enemies.ApplyEnemyState(msg);
	}
}
