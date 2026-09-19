using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Target → host: the verdict the target's own client reached by running the
/// target half of the operation's preconditions against its OWN live body. The
/// host commits the parked start on an accept and refuses the operator with the
/// target's own reason on a reject — the reason is a fact the answering client
/// observed, not an inference from a report.
/// </summary>
[ProtoContract]
public sealed class MedicalOperationTargetCheckAnswerMsg
{
	/// <summary>Echoes <see cref="MedicalOperationTargetCheckRequestMsg.RequestId"/>; an id with no parked request is dropped.</summary>
	[ProtoMember(1)]
	public ulong RequestId { get; set; }

	[ProtoMember(2)]
	public bool Accepted { get; set; }

	/// <summary>The answering client's own refusal reason, carried verbatim to the operator.</summary>
	[ProtoMember(3)]
	public string? RejectReason { get; set; }

	/// <summary>The target limb's LIVE shrapnel count, read at the same instant as the verdict — the shrapnel family builds its piece layout from this count instead of the stale report's.</summary>
	[ProtoMember(4)]
	public int ShrapnelCount { get; set; }
}
