using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Guest → host: the full reset a host instruction ordered was applied to the sender's own body,
/// and this is the resulting health/limb state. The authoritative write must not ride the ordinary
/// character report — that path preserves the three continuous values the host owns — so the target
/// sends them here instead, the same shape as the owner's limb/injury event.
///
/// <para>
/// The frame is a CLAIM and the service judges it against two facts this host holds: the payload's
/// owner must be the transport sender (the body belongs to its reporter's own client), and the
/// instruction id must be the one this host issued to that member and has not seen a result for.
/// Both checks live in <c>PlayerHealResetService.HandleApplied</c> so the control surface cannot be
/// entered from a second call site without them; a result that answers nothing writes nothing.
/// </para>
/// </summary>
[PacketHandler(NetMsg.HealResetApplied, NetMessageDirection.GuestToHost)]
internal sealed class HealResetAppliedHandler : PacketHandlerBase<HealResetAppliedMsg, IHealResetHandlerContext>
{
	protected override void Handle(ulong sender, HealResetAppliedMsg msg, IHealResetHandlerContext ctx) =>
		ctx.HealReset.HandleApplied(sender, msg);
}
