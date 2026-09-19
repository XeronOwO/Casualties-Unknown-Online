using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Host → member: the current partial block-damage records (world entry /
/// the 60 s resend), or the ANSWER to this side's own report
/// (<c>AnswersReport</c>). The receiver applies each entry to its own
/// deterministically-generated copy — the same semantic as the live
/// BlockDamaged relay, but for damage accumulated before the member joined —
/// and clears the cells' outstanding contributions only when the set is an
/// answer.
/// </summary>
[PacketHandler(NetMsg.BlockDamageSnapshot, NetMessageDirection.HostToGuest)]
public sealed class BlockDamageSnapshotHandler(ILogger<BlockDamageSnapshotHandler> log) : PacketHandlerBase<BlockDamageSnapshotMsg, IWorldHandlerContext>
{
	private readonly ILogger<BlockDamageSnapshotHandler> _log = log;

	protected override void Handle(ulong sender, BlockDamageSnapshotMsg msg, IWorldHandlerContext ctx)
	{
		_log.LogInformation("Block-damage snapshot received ({Count} cells, {Kind}).",
			msg.Entries.Count, msg.AnswersReport ? "an answer to this side's report" : "authoritative state");
		ctx.World.FireBlockDamageSnapshotReceived(msg.Entries, msg.Generation, msg.AnswersReport);
	}
}
