using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>Host → guest: the full enemy snapshot (world entry / late joiner) — ids + spawn positions for binding.</summary>
[PacketHandler(NetMsg.EnemySnapshot, NetMessageDirection.HostToGuest)]
public sealed class EnemySnapshotHandler : PacketHandlerBase<EnemySnapshotMsg>
{
	protected override void Handle(ulong sender, EnemySnapshotMsg msg, HandlerContext ctx)
	{
		if (ctx.Session.Role != SessionRole.Guest)
		{
			return;
		}

		ctx.Enemies.ApplyEnemySnapshot(msg);
	}
}
