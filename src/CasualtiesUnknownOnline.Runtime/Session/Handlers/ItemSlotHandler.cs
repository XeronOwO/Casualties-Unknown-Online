using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// An item moved between slots (SwapSlots / SwitchHands): guest → host report
/// carrying the new slot and the digest evidence — the host records it in the
/// item's transfer-table entry (the guest's own slot layout is its local fact,
/// never corrected) so the authoritative record stays current for corrections
/// and reconnects, and broadcasts the carried-fact event (the evidence when
/// there is no entry — a starting-supply item).
/// </summary>
[PacketHandler(NetMsg.ItemSlot)]
public sealed class ItemSlotHandler(ILogger<ItemSlotHandler> log) : PacketHandlerBase<ItemSlotMsg>
{
	private readonly ILogger<ItemSlotHandler> _log = log;

	protected override void Handle(ulong sender, ItemSlotMsg msg, HandlerContext ctx)
	{
		ctx.Items.FireItemSlotReceived(sender, msg.ItemId, msg.SlotIndex, msg.Item);
		_log.LogInformation("Item slot {ItemId} → {Slot} from {Sender}.", msg.ItemId, msg.SlotIndex, sender);
	}
}
