using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Block damage, star semantics (local compute → report → arbitrate → fan-out).
/// The handler only surfaces the message — the ARBITRATION lives in the world
/// domain (the Game Adapter): a break report (drops attached) is accepted when
/// the sender's own BlockPlaced already applied the break on the host
/// (first-writer-wins — the host records the applied air-write when it lands),
/// otherwise the drops are refused with an ItemReject and the breaker destroys
/// them. The accepted relay goes out through the world service's broadcast —
/// the source excluded, it already applied locally (the adapter's reentry guard
/// keeps the local application from echoing a new report). Guest: the host's
/// broadcast — apply it.
/// <para>
/// A partial-damage report additionally carries its sender's cumulative
/// contribution on that cell: the world layer resolves it against the per-sender
/// ledger BEFORE the domain sees it and raises the event with the increment this
/// host has not accounted for — a repeat, a stale frame or a duplicate report
/// raises nothing at all.
/// </para>
/// </summary>
[PacketHandler(NetMsg.BlockDamaged, NetMessageDirection.Bidirectional)]
public sealed class BlockDamagedHandler : PacketHandlerBase<BlockDamagedMsg, IWorldHandlerContext>
{
	protected override void Handle(ulong sender, BlockDamagedMsg msg, IWorldHandlerContext ctx) =>
		ctx.World.FireBlockDamagedReceived(sender, msg.X, msg.Y, msg.Damage, msg.MetalBonus, msg.Drops, msg.BuildingDrops, msg.Contribution, msg.Generation);
}
