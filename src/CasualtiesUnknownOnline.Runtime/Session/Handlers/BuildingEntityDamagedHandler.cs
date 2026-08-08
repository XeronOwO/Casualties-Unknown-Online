using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// A player's attack damaged a building entity, star semantics (local compute
/// → report → arbitrate → fan-out): the host applies the damage to its own
/// copy — which is what rolls the host-side entity drops — and relays to the
/// other members (the source excluded, it already applied locally). Guest:
/// the host's broadcast — apply it.
/// </summary>
[PacketHandler(NetMsg.BuildingEntityDamaged)]
public sealed class BuildingEntityDamagedHandler(ILogger<BuildingEntityDamagedHandler> log)
	: PacketHandlerBase<BuildingEntityDamagedMsg>
{
	private readonly ILogger<BuildingEntityDamagedHandler> _log = log;

	protected override void Handle(ulong sender, BuildingEntityDamagedMsg msg, HandlerContext ctx)
	{
		var pos = msg.Position.ToNetVector2();
		ctx.World.FireBuildingEntityDamagedReceived(pos, msg.Damage);
		if (ctx.Session.Role == SessionRole.Host)
		{
			ctx.Session.BroadcastExcept(sender, NetMsg.BuildingEntityDamaged, msg);
		}

		_log.LogInformation("Building entity damaged at {Pos} for {Damage} by {Sender}.", msg.Position, msg.Damage, sender);
	}
}
