using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// The host's authoritative trap layout arrived (world entry): the guest
/// aligns its regenerated world — materialize the missing entries (prefab
/// name), destroy the surplus/off-position entities. Host → guest only
/// (direction-validated by PacketReceiver). The snapshot's world/layer
/// generation rides along: the receiving seam refuses a snapshot of another
/// generation before any entry is materialized.
/// </summary>
[PacketHandler(NetMsg.TrapLayoutSnapshot, NetMessageDirection.HostToGuest)]
public sealed class TrapLayoutSnapshotHandler : PacketHandlerBase<TrapLayoutSnapshotMsg, IWorldHandlerContext>
{
	protected override void Handle(ulong sender, TrapLayoutSnapshotMsg msg, IWorldHandlerContext ctx) =>
		ctx.World.FireTrapLayoutReceived(sender, msg.Generation, msg.Entries);
}
