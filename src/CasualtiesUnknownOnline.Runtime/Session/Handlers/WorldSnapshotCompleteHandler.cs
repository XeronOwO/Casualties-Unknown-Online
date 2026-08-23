using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Host → guest: the world-entry snapshot group is complete. The receiver can
/// now distinguish a full authoritative world backfill from a partial
/// best-effort snapshot set.
/// </summary>
[PacketHandler(NetMsg.WorldSnapshotComplete, NetMessageDirection.HostToGuest)]
public sealed class WorldSnapshotCompleteHandler : PacketHandlerBase<WorldSnapshotCompleteMsg>
{
	protected override void Handle(ulong sender, WorldSnapshotCompleteMsg msg, HandlerContext ctx) =>
		ctx.World.FireWorldSnapshotCompleteReceived();
}
