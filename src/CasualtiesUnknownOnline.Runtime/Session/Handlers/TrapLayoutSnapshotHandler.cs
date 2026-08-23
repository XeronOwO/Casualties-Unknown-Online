using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// The host's authoritative trap layout arrived (world entry): the guest
/// aligns its regenerated world — materialize the missing entries (prefab
/// name), destroy the surplus/off-position entities. Host → guest only
/// (direction-validated by PacketReceiver).
/// </summary>
[PacketHandler(NetMsg.TrapLayoutSnapshot, NetMessageDirection.HostToGuest)]
public sealed class TrapLayoutSnapshotHandler : PacketHandlerBase<TrapLayoutSnapshotMsg, IWorldHandlerContext>
{
	protected override void Handle(ulong sender, TrapLayoutSnapshotMsg msg, IWorldHandlerContext ctx) =>
		ctx.World.FireTrapLayoutReceived(msg.Entries);
}
