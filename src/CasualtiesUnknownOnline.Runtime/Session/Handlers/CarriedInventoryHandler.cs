using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// A guest's carried inventory with self-assigned ids (its local generation
/// finished): the host registers the entries in the guest's transfer table —
/// the authoritative record that makes the guest's use/slot reports arbitrate
/// normally.
/// </summary>
[PacketHandler(NetMsg.CarriedInventory)]
public sealed class CarriedInventoryHandler(ILogger<CarriedInventoryHandler> log) : PacketHandlerBase<CarriedInventoryMsg>
{
	private readonly ILogger<CarriedInventoryHandler> _log = log;

	protected override void Handle(ulong sender, CarriedInventoryMsg msg, HandlerContext ctx)
	{
		ctx.Items.FireCarriedInventoryReceived(sender, msg.Items);
		_log.LogInformation("Carried inventory of {Sender}: {Count} items.", sender, msg.Items.Count);
	}
}
