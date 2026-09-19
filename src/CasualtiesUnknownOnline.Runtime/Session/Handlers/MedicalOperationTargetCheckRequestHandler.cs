using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

[PacketHandler(NetMsg.MedicalOperationTargetCheckRequest, NetMessageDirection.HostToGuest)]
internal sealed class MedicalOperationTargetCheckRequestHandler : PacketHandlerBase<MedicalOperationTargetCheckRequestMsg, IPlayerInteractionHandlerContext>
{
	protected override void Handle(ulong sender, MedicalOperationTargetCheckRequestMsg msg, IPlayerInteractionHandlerContext ctx) =>
		ctx.PlayerInteraction.MedicalOperations.HandleTargetCheckRequest(sender, msg);
}
