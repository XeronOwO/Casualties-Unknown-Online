using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// The host's absolute runtime-created entity table arrived (world entry or the
/// 60 s re-broadcast): the guest applies every entry through the live creation
/// path (missing entities materialize, existing ones bind by their creation
/// key), treats each entry as the host's acknowledgement of a matching pending
/// report, and drops the pending reports whose animal keys the snapshot
/// acknowledges (an accepted animal is never materialized from the snapshot —
/// the enemy domain owns that copy).
/// Host → guest only (direction-validated by PacketReceiver).
/// </summary>
[PacketHandler(NetMsg.RuntimeEntitySnapshot, NetMessageDirection.HostToGuest)]
public sealed class RuntimeEntitySnapshotHandler : PacketHandlerBase<RuntimeEntitySnapshotMsg, IWorldHandlerContext>
{
	protected override void Handle(ulong sender, RuntimeEntitySnapshotMsg msg, IWorldHandlerContext ctx) =>
		ctx.World.FireRuntimeEntitySnapshotReceived(sender, msg);
}
