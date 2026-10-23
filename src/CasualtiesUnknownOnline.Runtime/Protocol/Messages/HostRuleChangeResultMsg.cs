using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Host → requester: the answer to one <c>/gamerule</c> request, one sentence both
/// consoles print. The member's console has nothing else to print — the host is where
/// its request landed — so a refusal that never travelled would be indistinguishable
/// from a command that silently did nothing.
///
/// <para>
/// The sentence names the value IN FORCE, read back from the host's own rules surface
/// after the write, so a config layer that clamps or refuses the write can never make
/// the answer claim a value the entry does not hold.
/// </para>
/// </summary>
[ProtoContract]
public sealed class HostRuleChangeResultMsg
{
	/// <summary>True when the host applied the change; false when it refused, with <see cref="Answer"/> naming the ground.</summary>
	[ProtoMember(1)]
	public bool Applied { get; set; }

	/// <summary>The host's own sentence: the value now in force, or the refusal and its ground.</summary>
	[ProtoMember(2)]
	public string Answer { get; set; } = "";
}
