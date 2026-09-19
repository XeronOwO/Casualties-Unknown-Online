using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Guest → host: this sender's cumulative partial block-damage CONTRIBUTION for
/// the cells whose live delta report this host never accounted for
/// (sync-coverage audit W2, per sender since protocol 32). The host resolves each
/// row against its per-sender ledger, applies only the difference through the
/// same path a live delta takes, and answers with its own value for every
/// reported cell through the existing <see cref="NetMsg.BlockDamageSnapshot"/> —
/// that answer, not the periodic snapshot, is what clears the reporter's
/// outstanding entries.
/// </summary>
[PacketHandler(NetMsg.BlockDamageReport, NetMessageDirection.GuestToHost)]
public sealed class BlockDamageReportHandler(ILogger<BlockDamageReportHandler> log) : PacketHandlerBase<BlockDamageSnapshotMsg, IWorldHandlerContext>
{
	private readonly ILogger<BlockDamageReportHandler> _log = log;

	protected override void Handle(ulong sender, BlockDamageSnapshotMsg msg, IWorldHandlerContext ctx)
	{
		_log.LogInformation("Partial block-damage report received from {Peer} ({Count} cells).",
			sender, msg.Entries.Count);
		ctx.World.HandleBlockDamageReport(sender, msg.Entries, msg.Generation);
	}
}
