using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// An item moved between slots (SwapSlots / SwitchHands): guest → host report
/// carrying the new slot — the host records it in the item's transfer-table
/// entry (the guest's own slot layout is its local fact, never corrected) so
/// the authoritative record stays current for corrections and reconnects.
/// </summary>
[PacketHandler(NetMsg.ItemSlot)]
public sealed class ItemSlotHandler(ILogger<ItemSlotHandler> log) : PacketHandlerBase<ItemSlotMsg>
{
	private readonly ILogger<ItemSlotHandler> _log = log;

	protected override void Handle(ulong sender, ItemSlotMsg msg, HandlerContext ctx)
	{
		ctx.Items.FireItemSlotReceived(sender, msg.ItemId, msg.SlotIndex);
		_log.LogInformation("Item slot {ItemId} → {Slot} from {Sender}.", msg.ItemId, msg.SlotIndex, sender);
	}
}
