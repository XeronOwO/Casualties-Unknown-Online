using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

[PacketHandler(NetMsg.MedicalOperationCancel, NetMessageDirection.GuestToHost)]
internal sealed class MedicalOperationCancelHandler : PacketHandlerBase<MedicalOperationCancelMsg, IPlayerInteractionHandlerContext>
{
	protected override void Handle(ulong sender, MedicalOperationCancelMsg msg, IPlayerInteractionHandlerContext ctx) =>
		ctx.PlayerInteraction.MedicalOperations.HandleCancelRequest(sender, msg);
}
