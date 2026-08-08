using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Block damage, star semantics (local compute → report → arbitrate → fan-out):
/// host applies the report and relays it to the other members — the source is
/// excluded, it already applied locally (the adapter's reentry guard keeps the
/// local application from echoing a new report). Arbitration is the silent tier:
/// DamageBlock is idempotent on already-broken blocks, first-writer-wins is
/// automatic. Guest: the host's broadcast — apply it.
/// </summary>
[PacketHandler(NetMsg.BlockDamaged)]
public sealed class BlockDamagedHandler : PacketHandlerBase<BlockDamagedMsg>
{
	protected override void Handle(ulong sender, BlockDamagedMsg msg, HandlerContext ctx)
	{
		var pos = msg.Position.ToNetVector2();
		var session = ctx.Session;
		ctx.World.FireBlockDamagedReceived(pos, msg.Damage);
		if (session.Role == SessionRole.Host)
		{
			session.BroadcastExcept(sender, NetMsg.BlockDamaged, msg);
		}
	}
}
