using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

[PacketHandler(NetMsg.MedicalOperationTargetCheckAnswer, NetMessageDirection.GuestToHost)]
internal sealed class MedicalOperationTargetCheckAnswerHandler : PacketHandlerBase<MedicalOperationTargetCheckAnswerMsg, IPlayerInteractionHandlerContext>
{
	protected override void Handle(ulong sender, MedicalOperationTargetCheckAnswerMsg msg, IPlayerInteractionHandlerContext ctx) =>
		ctx.PlayerInteraction.MedicalOperations.HandleTargetCheckAnswer(sender, msg);
}
