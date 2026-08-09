using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// The host's generation-time item snapshot arrived (world entry / layer
/// switch): the ground items with host-assigned ids plus the starting-supplies
/// entries. Forwarded raw — the adapter decides per entry whether to bind a
/// local copy (world items by position, carried items by slot), materialize the
/// host's version or destroy a host-unknown local copy.
/// </summary>
[PacketHandler(NetMsg.WorldItemsSnapshot)]
public sealed class WorldItemsSnapshotHandler(ILogger<WorldItemsSnapshotHandler> log) : PacketHandlerBase<WorldItemsSnapshotMsg>
{
	private readonly ILogger<WorldItemsSnapshotHandler> _log = log;

	protected override void Handle(ulong sender, WorldItemsSnapshotMsg msg, HandlerContext ctx)
	{
		ctx.Items.FireWorldItemsSnapshotReceived(sender, msg.Items, msg.LayerModifierIndex, msg.LayerModifierRandomState);
		_log.LogInformation("Generation-item snapshot ({Count} items, modifier {Modifier}) from {Sender}.", msg.Items.Count, msg.LayerModifierIndex, sender);
	}
}
