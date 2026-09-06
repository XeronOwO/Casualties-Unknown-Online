using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

[PacketHandler(NetMsg.MedicalOperationEndRequest, NetMessageDirection.GuestToHost)]
internal sealed class MedicalOperationEndRequestHandler : PacketHandlerBase<MedicalOperationEndRequestMsg, IPlayerInteractionHandlerContext>
{
	protected override void Handle(ulong sender, MedicalOperationEndRequestMsg msg, IPlayerInteractionHandlerContext ctx) =>
		ctx.PlayerInteraction.MedicalOperations.HandleEndRequest(sender, msg);
}
