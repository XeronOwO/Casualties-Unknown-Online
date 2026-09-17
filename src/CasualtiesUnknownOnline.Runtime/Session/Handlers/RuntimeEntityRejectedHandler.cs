using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// The host rejected a runtime creation this side reported (decision 161):
/// surface it so the channel drops the matching pending re-report and the
/// adapter destroys the local copy through the normal death funnel. Only the
/// reporter's own creation key is acted on — a rejection that reaches another
/// member changes nothing there.
/// Host → guest only (direction-validated by PacketReceiver).
/// </summary>
[PacketHandler(NetMsg.RuntimeEntityRejected, NetMessageDirection.HostToGuest)]
public sealed class RuntimeEntityRejectedHandler : PacketHandlerBase<RuntimeEntityRejectedMsg, IWorldHandlerContext>
{
	protected override void Handle(ulong sender, RuntimeEntityRejectedMsg msg, IWorldHandlerContext ctx) =>
		ctx.World.FireRuntimeEntityRejectedReceived(sender, msg);
}
