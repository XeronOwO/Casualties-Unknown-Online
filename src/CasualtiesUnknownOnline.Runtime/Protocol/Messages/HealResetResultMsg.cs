using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Host → requester: the answer to one <c>/heal</c> request, one sentence both consoles print. A
/// member's console has nothing else to print — the host is where its request landed — so a refusal
/// that never travelled would be indistinguishable from a command that silently did nothing.
/// </summary>
[ProtoContract]
public sealed class HealResetResultMsg
{
	/// <summary>True when at least one target was reset (or instructed) and no target was refused.</summary>
	[ProtoMember(1)]
	public bool Applied { get; set; }

	/// <summary>The host's own sentence: what was reset, revived or restored, or the refusal and its ground.</summary>
	[ProtoMember(2)]
	public string Answer { get; set; } = "";
}
