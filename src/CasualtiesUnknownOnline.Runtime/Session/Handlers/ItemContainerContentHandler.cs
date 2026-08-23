using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// A carried container's full fact changed internally (a nested-content move):
/// guest → host report — the host records the parent fact in the transfer table
/// and broadcasts the carried-fact event (ItemCarriedSync) so the peers'
/// clones re-render the container's new contents immediately.
/// </summary>
[PacketHandler(NetMsg.ItemContainerContent, NetMessageDirection.GuestToHost)]
public sealed class ItemContainerContentHandler(ILogger<ItemContainerContentHandler> log) : PacketHandlerBase<ItemContainerContentMsg, IItemHandlerContext>
{
	private readonly ILogger<ItemContainerContentHandler> _log = log;

	protected override void Handle(ulong sender, ItemContainerContentMsg msg, IItemHandlerContext ctx)
	{
		ctx.Items.FireItemContainerContentReceived(sender, msg.ItemId, msg.Item);
		_log.LogInformation("Item container content {ItemId} from {Sender} ({Count} contents).", msg.ItemId, sender, msg.Item.Contents.Count);
	}
}
