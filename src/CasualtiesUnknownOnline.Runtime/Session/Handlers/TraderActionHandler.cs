using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// A guest's trader interaction arrived (it executed the game method locally —
/// the player-side effects are already there) — the host executes the
/// trader-side state change (TradeExecutor) and broadcasts the authoritative
/// state to every member (the acting side included — its local state was
/// provisional and is overwritten).
/// </summary>
[PacketHandler(NetMsg.TraderAction)]
public sealed class TraderActionHandler(ILogger<TraderActionHandler> log)
	: PacketHandlerBase<TraderActionMsg>
{
	private readonly ILogger<TraderActionHandler> _log = log;

	protected override void Handle(ulong sender, TraderActionMsg msg, HandlerContext ctx)
	{
		ctx.World.FireTraderActionReceived(sender, msg);
		_log.LogInformation("[Trade] action received from={Sender} action={Action} trader=({X:0.0},{Y:0.0}).",
			sender, msg.Action, msg.Position.X, msg.Position.Y);
	}
}
