using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// A player's limb latch changed, star semantics (local compute → report →
/// apply → fan-out): the host merges the post-event full limb/body state into
/// its record of the owner and relays to the other members (the source
/// excluded — it already applied locally). Guest: the host's relay — apply it.
/// Accept-first: the host adopts the reported state unconditionally (the body
/// belongs to the reporter's own local simulation — no validation); the 1 Hz
/// character snapshot stays the fallback. Mirror of <see cref="EnemyBiteHandler"/>.
/// </summary>
[PacketHandler(NetMsg.LimbStateEvent)]
public sealed class LimbStateEventHandler(ILogger<LimbStateEventHandler> log) : PacketHandlerBase<LimbStateEventMsg>
{
	private readonly ILogger<LimbStateEventHandler> _log = log;

	protected override void Handle(ulong sender, LimbStateEventMsg msg, HandlerContext ctx)
	{
		if (ctx.Session.Role == SessionRole.Host)
		{
			ctx.CharacterData.ApplyLimbStateEvent(msg);
		}

		ctx.CharacterData.FireLimbStateEventReceived(sender, msg);
		if (ctx.Session.Role == SessionRole.Host)
		{
			ctx.Session.BroadcastExcept(sender, NetMsg.LimbStateEvent, msg);
		}

		_log.LogInformation("[LimbEvent] owner {Owner} ({Limbs} limbs) from {Sender}.", msg.OwnerSteamId, msg.Limbs.Count, sender);
	}
}
