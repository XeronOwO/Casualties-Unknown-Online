using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

[PacketHandler(NetMsg.MedicalOperationStartAck, NetMessageDirection.HostToGuest)]
internal sealed class MedicalOperationStartAckHandler : PacketHandlerBase<MedicalOperationStartAckMsg, IPlayerInteractionHandlerContext>
{
	protected override void Handle(ulong sender, MedicalOperationStartAckMsg msg, IPlayerInteractionHandlerContext ctx) =>
		ctx.PlayerInteraction.MedicalOperations.FireStartAckReceived(msg);
}
