using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// A trader's authoritative state arrived (the host computed it — an
/// interaction, the world-entry snapshot, or the 5 s fallback) — the guest
/// applies the full overwrite onto its local trader (stock + sync fields, UI
/// refresh, the rejected-purchase rollback).
/// </summary>
[PacketHandler(NetMsg.TraderState)]
public sealed class TraderStateHandler(ILogger<TraderStateHandler> log)
	: PacketHandlerBase<TraderStateMsg>
{
	private readonly ILogger<TraderStateHandler> _log = log;

	protected override void Handle(ulong sender, TraderStateMsg msg, HandlerContext ctx)
	{
		ctx.World.FireTraderStateReceived(msg);
		_log.LogInformation("[Trade] state received trader=({X:0.0},{Y:0.0}) rep={Rep} items={N} reject={Reject}.",
			msg.Position.X, msg.Position.Y, msg.Reputation, msg.Items.Length, msg.RejectedAction);
	}
}
