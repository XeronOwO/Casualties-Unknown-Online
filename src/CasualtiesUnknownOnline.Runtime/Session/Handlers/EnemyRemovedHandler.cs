using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>Host → guest: one enemy aggregate left the authoritative host set.</summary>
[PacketHandler(NetMsg.EnemyRemoved, NetMessageDirection.HostToGuest)]
public sealed class EnemyRemovedHandler : PacketHandlerBase<EnemyRemovedMsg, IEnemySessionHandlerContext>
{
	protected override void Handle(ulong sender, EnemyRemovedMsg msg, IEnemySessionHandlerContext ctx)
	{
		if (ctx.Session.Role != SessionRole.Guest)
		{
			return;
		}

		ctx.Enemies.ApplyEnemyRemoved(msg);
	}
}
