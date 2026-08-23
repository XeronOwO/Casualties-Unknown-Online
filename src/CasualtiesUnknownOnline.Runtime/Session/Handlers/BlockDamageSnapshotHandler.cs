using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Host → member: the current partial block-damage records (world entry /
/// the 60 s resend). The receiver applies each entry to its own
/// deterministically-generated copy — the same semantic as the live
/// BlockDamaged relay, but for damage accumulated before the member joined.
/// </summary>
[PacketHandler(NetMsg.BlockDamageSnapshot, NetMessageDirection.HostToGuest)]
public sealed class BlockDamageSnapshotHandler(ILogger<BlockDamageSnapshotHandler> log) : PacketHandlerBase<BlockDamageSnapshotMsg>
{
	private readonly ILogger<BlockDamageSnapshotHandler> _log = log;

	protected override void Handle(ulong sender, BlockDamageSnapshotMsg msg, HandlerContext ctx)
	{
		_log.LogInformation("Block-damage snapshot received ({Count} cells).", msg.Entries.Count);
		ctx.World.FireBlockDamageSnapshotReceived(msg.Entries);
	}
}
