using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Host 鈫?requester: the answer to a layer-advance request the host could not take. The member suppressed its
/// own descent when it asked, so this line is what tells it the layer did not move; the channel trusts it only
/// from this session's host, so a forged answer cannot print attacker-chosen text on a member's log.
/// </summary>
[PacketHandler(NetMsg.LayerAdvanceRefused, NetMessageDirection.HostToGuest)]
public sealed class LayerAdvanceRefusedHandler : PacketHandlerBase<LayerAdvanceRefusedMsg, ILayerAdvanceHandlerContext>
{
	protected override void Handle(ulong sender, LayerAdvanceRefusedMsg msg, ILayerAdvanceHandlerContext ctx) =>
		ctx.LayerAdvance.HandleLayerAdvanceRefused(sender, msg);
}
