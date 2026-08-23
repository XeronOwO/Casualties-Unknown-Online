using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// The host's authoritative item state: our last action-report evidence
/// diverged from its record (wrong contents, condition, liquids, components or
/// slot) — the action itself was accepted, the divergence is synced now. The
/// adapter applies the payload via its restore machinery.
/// </summary>
[PacketHandler(NetMsg.ItemCorrection, NetMessageDirection.HostToGuest)]
public sealed class ItemCorrectionHandler(ILogger<ItemCorrectionHandler> log) : PacketHandlerBase<ItemCorrectionMsg, IItemHandlerContext>
{
	private readonly ILogger<ItemCorrectionHandler> _log = log;

	protected override void Handle(ulong sender, ItemCorrectionMsg msg, IItemHandlerContext ctx)
	{
		ctx.Items.FireItemCorrectionReceived(sender, msg.Item);
		_log.LogInformation("Item correction received from {Sender}: {Type} (Instance {InstanceId}).", sender, msg.Item.ItemId, msg.Item.InstanceId);
	}
}
