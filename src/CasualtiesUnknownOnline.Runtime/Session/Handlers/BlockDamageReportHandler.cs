using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Guest → host: the guest's ABSOLUTE partial block-damage set for the cells
/// whose live delta report this host never answered (sync-coverage audit W2).
/// The host merges every row into the GAME's own <c>blockDamages</c> list through
/// the native port — per cell, never below what it already holds — and answers
/// with its authoritative value for every reported cell through the existing
/// <see cref="NetMsg.BlockDamageSnapshot"/>, which is what clears the reporter's
/// pending entry.
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
