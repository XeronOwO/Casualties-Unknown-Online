using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// An item was used (Body.UseItem): guest → host report carrying the used
/// state as digest evidence — the host validates it against the item's
/// transfer-table entry, adopts it when it matches (the guest is the fact
/// source for its own body) and sends an ItemCorrection when it diverges.
/// Usage itself is never rejected.
/// </summary>
[PacketHandler(NetMsg.ItemUse)]
public sealed class ItemUseHandler(ILogger<ItemUseHandler> log) : PacketHandlerBase<ItemUseMsg>
{
	private readonly ILogger<ItemUseHandler> _log = log;

	protected override void Handle(ulong sender, ItemUseMsg msg, HandlerContext ctx)
	{
		ctx.Items.FireItemUseReceived(sender, msg.ItemId, msg.Item);
		_log.LogInformation("Item use {ItemId} from {Sender}.", msg.ItemId, sender);
	}
}
