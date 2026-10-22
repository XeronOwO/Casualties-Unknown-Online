using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;
using CasualtiesUnknownOnline.Runtime.Session.CharacterData;
using Microsoft.Extensions.Logging;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// A player's limb latch changed, star semantics (local compute → report →
/// apply → fan-out): the host merges the post-event full limb/body state into
/// its record of the owner and relays to the other members (the source
/// excluded — it already applied locally). Guest: the host's relay — apply it.
/// Accept-first: the host adopts the reported state unconditionally (the body
/// belongs to the reporter's own local simulation); the 1 Hz
/// character snapshot stays the fallback. Mirror of the <c>EnemyBiteMsg</c> path.
///
/// <para>
/// The one thing accept-first may not accept is WHOSE body this is: the payload's
/// <c>OwnerSteamId</c> is a claim, and the body it names belongs to the reporter's
/// own simulation (<c>CharacterDataSync.CaptureLimbStateEvent</c> always stamps
/// <c>_session.LocalSteamId</c>), so on the host an inbound frame naming somebody
/// else is dropped — nothing durable is written and nothing is relayed. The
/// comparison is host-side only: the relay the host sends carries the OWNER's id
/// while its transport sender is the host, so a guest must not judge it. No local
/// exemption is needed on the host's side either — the local id never enters the
/// presence table, so every frame this handler sees on the host came from a member.
/// What that leaves unbounded is named in the census row: a forged frame addressed
/// straight at a GUEST is applied there as presentation, which the owner's own next
/// report overwrites and no durable record keeps.
/// </para>
/// </summary>
[PacketHandler(NetMsg.LimbStateEvent, NetMessageDirection.Bidirectional)]
public sealed class LimbStateEventHandler(ILogger<LimbStateEventHandler> log) : PacketHandlerBase<LimbStateEventMsg, ICharacterSessionHandlerContext>
{
	private readonly ILogger<LimbStateEventHandler> _log = log;

	protected override void Handle(ulong sender, LimbStateEventMsg msg, ICharacterSessionHandlerContext ctx)
	{
		if (ctx.Session.Role == SessionRole.Host && msg.OwnerSteamId != sender)
		{
			_log.LogWarning("[LimbEvent] dropped spoofed latch from {Sender} claiming owner {Owner}.", sender, msg.OwnerSteamId);
			return;
		}

		if (ctx.Session.Role == SessionRole.Host)
		{
			// The store's merge can refuse a non-finite event; the handler must then skip BOTH the
			// adapter event and the relay — the adapter would apply the refused limbs to the
			// reporter's clone and the relay would push them to every guest.
			if (!ctx.CharacterData.ApplyLimbStateEvent(msg))
			{
				_log.LogWarning("[LimbEvent] refused a non-finite owner event from {Sender} for {Owner}.", sender, msg.OwnerSteamId);
				return;
			}
		}
		else if (CharacterDataFinite.FirstNonFinite(new CharacterDataMsg { Health = msg.Health, Limbs = msg.Limbs }) is { } field)
		{
			// A guest receives the host's relay; a refused event must never reach its presentation.
			_log.LogWarning("[LimbEvent] dropped a non-finite relay from the host for {Owner}: {Field}.", msg.OwnerSteamId, field);
			return;
		}

		ctx.CharacterData.FireLimbStateEventReceived(sender, msg);
		if (ctx.Session.Role == SessionRole.Host)
		{
			ctx.Session.BroadcastExcept(sender, NetMsg.LimbStateEvent, msg);
		}

		_log.LogInformation("[LimbEvent] owner {Owner} ({Limbs} limbs) from {Sender}.", msg.OwnerSteamId, msg.Limbs.Count, sender);
	}
}
