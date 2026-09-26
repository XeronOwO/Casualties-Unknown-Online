using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// A world-item impact presentation the AUTHORITY side played natively (the
/// <c>drop</c> clip, the landing block's step sound and the dust — or a
/// plush's squeak). Host → guest only: a guest's world-item copies are
/// non-authoritative and their collision presentation is suppressed, so the
/// reporting side is always the one that still simulates the landing. The
/// handler fires the received event so the Game Adapter replays the
/// presentation on its own world; there is nothing to relay from a guest.
/// </summary>
[PacketHandler(NetMsg.ItemImpact, NetMessageDirection.HostToGuest)]
public sealed class ItemImpactHandler(ILogger<ItemImpactHandler> log) : PacketHandlerBase<ItemImpactMsg, IItemHandlerContext>
{
	private readonly ILogger<ItemImpactHandler> _log = log;

	protected override void Handle(ulong sender, ItemImpactMsg msg, IItemHandlerContext ctx)
	{
		ctx.Items.FireItemImpactReceived(sender, msg);

		_log.LogDebug("[ItemImpact] kind={Kind} at ({X:0.0},{Y:0.0}) index={Index} from {Sender}.",
			msg.Kind, msg.Position.X, msg.Position.Y, msg.SoundIndex, sender);
	}
}
