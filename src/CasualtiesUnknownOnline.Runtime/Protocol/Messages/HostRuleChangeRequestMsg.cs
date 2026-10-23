using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Guest → host: one <c>/gamerule</c> write a member asks the host to perform, the
/// rule by the census id it names and the value in the canonical text form the
/// member's own parser produced.
///
/// <para>
/// It is a REQUEST, not a report: the host executes the write on its own config entry
/// against its own rules surface, so the permission (the host's cheat switch and its
/// <c>/op</c> roster), the rule's existence and the value's grammar are all judged
/// where the write happens, and the member's word is only the intent. The member's
/// local parse exists to refuse an invalid value without a round trip, never to
/// authorise the write: the host re-parses the same canonical text, so a request this
/// build's grammar does not accept is refused by name instead of written.
/// </para>
///
/// <para>
/// The member never writes its own config from this verb: its local entries would be
/// overridden by the host's announced set (decision 263), so the change would look
/// applied and do nothing. The answer therefore rides
/// <see cref="HostRuleChangeResultMsg"/> and the announcement carries the outcome to
/// every other member.
/// </para>
/// </summary>
[ProtoContract]
public sealed class HostRuleChangeRequestMsg
{
	/// <summary>The rule's census id as its <c>HostRuleId</c> value; the host resolves it against its own census and refuses an id it does not know.</summary>
	[ProtoMember(1)]
	public int Rule { get; set; }

	/// <summary>The canonical value text (<c>HostRuleValueText</c>'s form for the rule's kind); the host re-parses it with the same grammar before writing.</summary>
	[ProtoMember(2)]
	public string Value { get; set; } = "";
}
