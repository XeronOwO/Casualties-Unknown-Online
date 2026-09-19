using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Host → guest: enter-the-world instruction. Carries the entry kind
/// (tutorial vs run) so the guest starts the right run immediately, even
/// before the world params arrive — the guest's generation boundary then
/// waits for the params (the adapter owns that wait). It also carries the
/// host's kernel run epoch: this is the edge a new run legitimately begins
/// at, so the identity is recorded here and every checkpoint chunk set the
/// entry group sends is validated against it.
/// </summary>
[PacketHandler(NetMsg.WorldJoin, NetMessageDirection.HostToGuest)]
public sealed class WorldJoinHandler : PacketHandlerBase<WorldJoinMsg, IWorldKernelHandlerContext>
{
	protected override void Handle(ulong sender, WorldJoinMsg msg, IWorldKernelHandlerContext ctx)
	{
		// Recorded before the entry is raised: the set the entry group carries is
		// validated against this identity, so a set from another run is refused
		// instead of restored onto a member the live streams no longer match.
		ctx.KernelProtocol.AdoptHostRunEpoch(msg.RunEpoch);
		ctx.World.FireWorldJoinReceived(msg.IsTutorial);
	}
}
