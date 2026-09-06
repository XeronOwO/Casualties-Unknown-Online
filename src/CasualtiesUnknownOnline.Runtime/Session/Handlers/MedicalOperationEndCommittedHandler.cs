using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

[PacketHandler(NetMsg.MedicalOperationEndCommitted, NetMessageDirection.HostToGuest)]
internal sealed class MedicalOperationEndCommittedHandler : PacketHandlerBase<MedicalOperationEndCommittedMsg, IPlayerInteractionHandlerContext>
{
	protected override void Handle(ulong sender, MedicalOperationEndCommittedMsg msg, IPlayerInteractionHandlerContext ctx) =>
		ctx.PlayerInteraction.MedicalOperations.FireEndCommittedReceived(msg);
}
