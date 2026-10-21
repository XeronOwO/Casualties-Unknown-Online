using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>Host → guest: the full enemy roster (world entry / late joiner / the 60 s repair) — every row's id, prefab, match hint and current state.</summary>
[PacketHandler(NetMsg.EnemySnapshot, NetMessageDirection.HostToGuest)]
public sealed class EnemySnapshotHandler : PacketHandlerBase<EnemySnapshotMsg, IEnemySessionHandlerContext>
{
	protected override void Handle(ulong sender, EnemySnapshotMsg msg, IEnemySessionHandlerContext ctx)
	{
		if (ctx.Session.Role != SessionRole.Guest)
		{
			return;
		}

		ctx.Enemies.ApplyEnemySnapshot(sender, msg);
	}
}
