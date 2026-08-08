using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// An item the SENDER generated has settled: its generator-side physics is the
/// position authority, so the host updates the table entry and aligns its own
/// phantom to the reported spot (the receiver-side physics drifts otherwise —
/// the "item fell through the world" / "pulled back" class of bugs). Guest →
/// host only (direction-validated by PacketReceiver).
/// </summary>
[PacketHandler(NetMsg.ItemSettle)]
public sealed class ItemSettleHandler : PacketHandlerBase<ItemSettleMsg>
{
	protected override void Handle(ulong sender, ItemSettleMsg msg, HandlerContext ctx)
	{
		ctx.Items.FireItemSettleReceived(sender, msg.ItemId,
			msg.Position?.ToNetVector2() ?? NetVector2.Zero, msg.Rotation);
	}
}
