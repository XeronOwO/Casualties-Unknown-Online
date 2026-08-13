using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Host → member: the opened lockable entities' positions (world entry —
/// the receiver applies each open to its own deterministically-generated
/// copy, the same application as the live BuildingEntityOpened relay).
/// </summary>
[PacketHandler(NetMsg.OpenedEntitiesSnapshot)]
public sealed class OpenedEntitiesSnapshotHandler(ILogger<OpenedEntitiesSnapshotHandler> log) : PacketHandlerBase<OpenedEntitiesSnapshotMsg>
{
	private readonly ILogger<OpenedEntitiesSnapshotHandler> _log = log;

	protected override void Handle(ulong sender, OpenedEntitiesSnapshotMsg msg, HandlerContext ctx)
	{
		_log.LogInformation("Opened-entities snapshot received ({Count} positions).", msg.Positions.Count);
		ctx.World.FireOpenedEntitiesSnapshotReceived(msg.Positions);
	}
}
