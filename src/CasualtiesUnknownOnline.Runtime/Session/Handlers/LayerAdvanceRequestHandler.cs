using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Guest → host: a member reached the end of the layer and chose to continue
/// (member intent, host authority — the layer's baseline is the host's capture,
/// so the member asks the session to take the step instead of generating a layer
/// the session never agreed on). Admitted only from a handshaken member and only
/// when the request names the generation this host is in; what the request then
/// means for the world (drive the native entry, exactly once) is the world
/// domain's decision.
/// </summary>
[PacketHandler(NetMsg.LayerAdvanceRequest, NetMessageDirection.GuestToHost)]
public sealed class LayerAdvanceRequestHandler : PacketHandlerBase<LayerAdvanceRequestMsg, ILayerAdvanceHandlerContext>
{
	protected override void Handle(ulong sender, LayerAdvanceRequestMsg msg, ILayerAdvanceHandlerContext ctx) =>
		ctx.LayerAdvance.HandleLayerAdvanceRequest(sender, msg);
}
