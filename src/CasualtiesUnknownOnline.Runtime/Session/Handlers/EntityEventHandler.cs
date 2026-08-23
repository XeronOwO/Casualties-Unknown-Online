using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// A trap/mechanism event fired: the handler only surfaces the message. The
/// apply + relay live in the adapter's EntityEventSync (host applies to its
/// own world, records one-shot consumptions and broadcasts to the other
/// members) — a handler-level broadcast here would send a second copy.
/// </summary>
[PacketHandler(NetMsg.EntityEvent, NetMessageDirection.Bidirectional)]
public sealed class EntityEventHandler(ILogger<EntityEventHandler> log)
	: PacketHandlerBase<EntityEventMsg>
{
	private readonly ILogger<EntityEventHandler> _log = log;

	protected override void Handle(ulong sender, EntityEventMsg msg, HandlerContext ctx)
	{
		ctx.World.FireEntityEventReceived(sender, msg);

		_log.LogInformation("[TrapEvent] kind={Kind} pos={Pos} from {Sender}.", msg.Kind, msg.Position, sender);
	}
}
