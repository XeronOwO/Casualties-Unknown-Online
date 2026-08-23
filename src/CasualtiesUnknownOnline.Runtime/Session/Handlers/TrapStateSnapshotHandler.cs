using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// The host's one-shot trap consumptions arrived (world entry, alongside the
/// block-state snapshot) — the guest consumes each entry against its own
/// deterministic world (idempotent: an already-destroyed entity is skipped).
/// </summary>
[PacketHandler(NetMsg.TrapStateSnapshot, NetMessageDirection.HostToGuest)]
public sealed class TrapStateSnapshotHandler(ILogger<TrapStateSnapshotHandler> log)
	: PacketHandlerBase<TrapStateSnapshotMsg, IWorldHandlerContext>
{
	private readonly ILogger<TrapStateSnapshotHandler> _log = log;

	protected override void Handle(ulong sender, TrapStateSnapshotMsg msg, IWorldHandlerContext ctx)
	{
		ctx.World.FireTrapStateReceived(msg.Consumed);
		_log.LogInformation("[TrapSnapshot] received {Count} consumed.", msg.Consumed.Count);
	}
}
