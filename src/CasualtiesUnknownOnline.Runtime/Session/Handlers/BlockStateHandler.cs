using System.Linq;
using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.World;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Host → guest: the host's authoritative block-state snapshot (every block
/// deviating from the generated baseline). Applied once the guest's own
/// generation finished — the snapshot is only sent after the guest reported
/// InWorld (which requires generation complete), so ordering is guaranteed.
/// </summary>
[PacketHandler(NetMsg.WorldBlockState, NetMessageDirection.HostToGuest)]
public sealed class BlockStateHandler(ILogger<BlockStateHandler> log) : PacketHandlerBase<BlockStateMsg, IWorldHandlerContext>
{
	private readonly ILogger<BlockStateHandler> _log = log;

	protected override void Handle(ulong sender, BlockStateMsg msg, IWorldHandlerContext ctx)
	{
		if (msg.Blocks is not { Count: > 0 })
		{
			return;
		}

		var blocks = msg.Blocks
			.Select(b => new DamagedBlock(b.X, b.Y, b.Block))
			.ToList();
		ctx.World.FireBlockStateReceived(blocks);
		_log.LogInformation("Received block-state snapshot ({Count} blocks) from the host.", blocks.Count);
	}
}
