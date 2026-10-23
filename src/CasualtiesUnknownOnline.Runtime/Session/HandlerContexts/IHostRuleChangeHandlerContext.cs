using CasualtiesUnknownOnline.Runtime.Session.HostRules;

namespace CasualtiesUnknownOnline.Runtime.Session;

/// <summary>
/// The control surface used by the host-rule change pair: the session role an answer is
/// valid under (host → member only), and the domain that applies a member's request on the
/// host or raises the host's answer on the member.
/// </summary>
public interface IHostRuleChangeHandlerContext
{
	ISessionControl Session { get; }

	/// <summary>The console <c>/gamerule</c> write path: the host applies a request and answers, the member receives the answer.</summary>
	IHostRuleChangeControl HostRuleChanges { get; }
}
