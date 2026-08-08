using System.Collections.Generic;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.Items;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// The host's full world-item table arrived (world entry — late joiner or
/// reconnect). The guest reconciles its local world items against the
/// snapshot: spawns the missing, destroys the stale.
/// </summary>
[PacketHandler(NetMsg.ItemSnapshot)]
public sealed class ItemSnapshotHandler(ILogger<ItemSnapshotHandler> log) : PacketHandlerBase<ItemSnapshotMsg>
{
	private readonly ILogger<ItemSnapshotHandler> _log = log;

	protected override void Handle(ulong sender, ItemSnapshotMsg msg, HandlerContext ctx)
	{
		var items = new List<WorldItem>(msg.Entries.Count);
		foreach (var entry in msg.Entries)
		{
			items.Add(new WorldItem(entry.ItemId, entry.Item, entry.Position.ToNetVector2(), entry.Velocity.ToNetVector2()));
		}

		ctx.Items.FireItemSnapshotReceived(sender, items);
		_log.LogInformation("World-item snapshot ({Count} items) from {Sender}.", items.Count, sender);
	}
}
