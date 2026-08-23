using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// A player-lit dynamite detonated. Guest → host report: the host's adapter
/// applies the explosion to its own world and relays to the other guests;
/// host → guest broadcast: the receiving guest replays the explosion's
/// body/visual segment. The adapter owns apply/replay — the handler only
/// surfaces the event (same shape as EntityEventHandler).
/// </summary>
[PacketHandler(NetMsg.DynamiteExplosion, NetMessageDirection.Bidirectional)]
public sealed class DynamiteExplosionHandler(ILogger<DynamiteExplosionHandler> log) : PacketHandlerBase<DynamiteExplosionMsg, IWorldHandlerContext>
{
	private readonly ILogger<DynamiteExplosionHandler> _log = log;

	protected override void Handle(ulong sender, DynamiteExplosionMsg msg, IWorldHandlerContext ctx)
	{
		ctx.World.FireDynamiteExplosionReceived(sender, msg.ItemInstanceId, msg.Position.ToNetVector2());
		_log.LogInformation("Dynamite explosion item {ItemId} at ({X:F1},{Y:F1}) from {Sender}.",
			msg.ItemInstanceId, msg.Position.X, msg.Position.Y, sender);
	}
}
