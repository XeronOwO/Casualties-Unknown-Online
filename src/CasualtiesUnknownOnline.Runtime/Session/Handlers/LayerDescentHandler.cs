using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Host → guest: the host is advancing a layer now. The member starts its own
/// new-layer generation from the host's baseline immediately instead of being
/// pulled to the menu; the world domain decides whether this member is in a world
/// that can follow and drops the baseline it still holds so the generation waits
/// for the host's new one.
/// </summary>
[PacketHandler(NetMsg.LayerDescent, NetMessageDirection.HostToGuest)]
public sealed class LayerDescentHandler : PacketHandlerBase<LayerDescentMsg, IWorldHandlerContext>
{
	protected override void Handle(ulong sender, LayerDescentMsg msg, IWorldHandlerContext ctx) =>
		ctx.World.FireLayerDescentReceived(msg.Twice, msg.Generation);
}
