using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Item-id counter high-water mark, bidirectional: a guest's report (it
/// allocated up to Counter — the host records it) or the host's grant (the
/// guest must resume from Counter + 1). The host records; the guest applies.
/// </summary>
[PacketHandler(NetMsg.ItemIdWatermark, NetMessageDirection.Bidirectional)]
public sealed class ItemIdWatermarkHandler(ILogger<ItemIdWatermarkHandler> log) : PacketHandlerBase<ItemIdWatermarkMsg, IItemHandlerContext>
{
	private readonly ILogger<ItemIdWatermarkHandler> _log = log;

	protected override void Handle(ulong sender, ItemIdWatermarkMsg msg, IItemHandlerContext ctx)
	{
		ctx.Items.FireItemIdWatermarkReceived(sender, msg.Counter);
		_log.LogInformation("Item id watermark {Counter} from {Sender}.", msg.Counter, sender);
	}
}
