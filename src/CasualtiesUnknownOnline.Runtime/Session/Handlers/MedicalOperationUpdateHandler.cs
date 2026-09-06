using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

[PacketHandler(NetMsg.MedicalOperationUpdate, NetMessageDirection.GuestToHost)]
internal sealed class MedicalOperationUpdateHandler : PacketHandlerBase<MedicalOperationUpdateMsg, IPlayerInteractionHandlerContext>
{
	protected override void Handle(ulong sender, MedicalOperationUpdateMsg msg, IPlayerInteractionHandlerContext ctx) =>
		ctx.PlayerInteraction.MedicalOperations.HandleUpdate(sender, msg);
}
