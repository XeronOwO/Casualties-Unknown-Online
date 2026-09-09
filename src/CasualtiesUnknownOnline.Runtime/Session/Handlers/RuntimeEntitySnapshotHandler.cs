using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// The host's absolute runtime-created entity table arrived (world entry or the
/// 60 s re-broadcast): the guest applies every entry through the live creation
/// path (missing entities materialize, existing ones are deduped) and treats
/// each entry as the host's acknowledgement of a matching pending report.
/// Host → guest only (direction-validated by PacketReceiver).
/// </summary>
[PacketHandler(NetMsg.RuntimeEntitySnapshot, NetMessageDirection.HostToGuest)]
public sealed class RuntimeEntitySnapshotHandler : PacketHandlerBase<RuntimeEntitySnapshotMsg, IWorldHandlerContext>
{
	protected override void Handle(ulong sender, RuntimeEntitySnapshotMsg msg, IWorldHandlerContext ctx) =>
		ctx.World.FireRuntimeEntitySnapshotReceived(sender, msg.Entries);
}
