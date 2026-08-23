using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// A carried item's authoritative fact changed (host → guest): a use flipped
/// its component state, a slot move re-homed it, a pickup brought it into the
/// inventory. The receiver updates its per-player fact table entry and
/// re-renders that player's clone immediately — the 1 Hz character snapshot
/// stays as the fallback.
/// </summary>
[PacketHandler(NetMsg.ItemCarriedSync, NetMessageDirection.HostToGuest)]
public sealed class ItemCarriedSyncHandler(ILogger<ItemCarriedSyncHandler> log) : PacketHandlerBase<ItemCarriedSyncMsg, IItemHandlerContext>
{
	private readonly ILogger<ItemCarriedSyncHandler> _log = log;

	protected override void Handle(ulong sender, ItemCarriedSyncMsg msg, IItemHandlerContext ctx)
	{
		ctx.Items.FireItemCarriedSyncReceived(sender, msg.OwnerSteamId, msg.Item, msg.SlotKnown);
		_log.LogInformation("Carried sync for {Owner}'s {ItemId} (id {InstanceId}) from {Sender}.", msg.OwnerSteamId, msg.Item.ItemId, msg.Item.InstanceId, sender);
	}
}
