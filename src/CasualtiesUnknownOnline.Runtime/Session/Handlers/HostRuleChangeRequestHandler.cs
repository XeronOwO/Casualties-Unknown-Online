using CasualtiesUnknownOnline.Runtime.Protocol;
using CasualtiesUnknownOnline.Runtime.Protocol.Messages;

namespace CasualtiesUnknownOnline.Runtime.Session.Handlers;

/// <summary>
/// Guest → host: a member typed <c>/gamerule &lt;rule&gt; &lt;value&gt;</c>. The host judges its
/// own cheat switch and roster, re-parses the value with its own grammar against its own
/// census, writes the rule through the same config entry the Admin page and its own verb
/// use, and answers with the value the entry holds afterwards — the answer the member's
/// console prints where the verb was typed.
/// </summary>
[PacketHandler(NetMsg.HostRuleChangeRequest, NetMessageDirection.GuestToHost)]
internal sealed class HostRuleChangeRequestHandler : PacketHandlerBase<HostRuleChangeRequestMsg, IHostRuleChangeHandlerContext>
{
	protected override void Handle(ulong sender, HostRuleChangeRequestMsg msg, IHostRuleChangeHandlerContext ctx) =>
		ctx.HostRuleChanges.HandleRequest(sender, msg);
}
