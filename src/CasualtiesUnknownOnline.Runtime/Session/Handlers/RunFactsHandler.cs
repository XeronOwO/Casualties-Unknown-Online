using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Host → guest: the run clock base and the layer's radiation-timer accounting
/// (host authority — they are this process's own statics, which a member that
/// joined mid-run never had; the host's live world is where they are read).
/// The receiver refuses a message whose generation stamp is not its own.
/// </summary>
[PacketHandler(NetMsg.RunFacts, NetMessageDirection.HostToGuest)]
public sealed class RunFactsHandler : PacketHandlerBase<RunFactsMsg, IWorldHandlerContext>
{
	protected override void Handle(ulong sender, RunFactsMsg msg, IWorldHandlerContext ctx) =>
		ctx.World.FireRunFactsReceived(msg);
}
