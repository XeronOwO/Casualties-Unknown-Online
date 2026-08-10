using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// A trap/mechanism event fired, star semantics (local compute → report →
/// apply → fan-out): the host applies the event to its own world (the
/// TrapEffectApplier — an exploding mine destroys the host's copy and rolls
/// the host-side drops) and relays to the other members (the source excluded,
/// it already applied locally). Guest: the host's relay — replay the event.
/// </summary>
[PacketHandler(NetMsg.EntityEvent)]
public sealed class EntityEventHandler(ILogger<EntityEventHandler> log)
	: PacketHandlerBase<EntityEventMsg>
{
	private readonly ILogger<EntityEventHandler> _log = log;

	protected override void Handle(ulong sender, EntityEventMsg msg, HandlerContext ctx)
	{
		ctx.World.FireEntityEventReceived(sender, msg);
		if (ctx.Session.Role == SessionRole.Host)
		{
			ctx.Session.BroadcastExcept(sender, NetMsg.EntityEvent, msg);
		}

		_log.LogInformation("[TrapEvent] kind={Kind} pos={Pos} from {Sender}.", msg.Kind, msg.Position, sender);
	}
}
