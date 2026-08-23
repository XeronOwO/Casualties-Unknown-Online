using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// A lockable building entity was opened, star semantics (local compute →
/// report → arbitrate → fan-out): the host applies the open to its own copy —
/// which is what rolls the host-side entity drops — and relays to the other
/// members (the source excluded). Guest: the host's broadcast — apply it.
/// </summary>
[PacketHandler(NetMsg.BuildingEntityOpened, NetMessageDirection.Bidirectional)]
public sealed class BuildingEntityOpenedHandler(ILogger<BuildingEntityOpenedHandler> log)
	: PacketHandlerBase<BuildingEntityOpenedMsg>
{
	private readonly ILogger<BuildingEntityOpenedHandler> _log = log;

	protected override void Handle(ulong sender, BuildingEntityOpenedMsg msg, HandlerContext ctx)
	{
		var pos = msg.Position.ToNetVector2();
		ctx.World.FireBuildingEntityOpenedReceived(pos);
		if (ctx.Session.Role == SessionRole.Host)
		{
			ctx.Session.BroadcastExcept(sender, NetMsg.BuildingEntityOpened, msg);
		}

		_log.LogInformation("Building entity opened at {Pos} by {Sender}.", msg.Position, sender);
	}
}
