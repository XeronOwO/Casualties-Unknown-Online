using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Host 鈫?requester: the host could not take the member's layer-advance request. The member suppressed its own
/// descent when it asked, so the refusal has to be answered or the member cannot tell a lost request from a
/// host that could not move the layer.
/// </summary>
[ProtoContract]
public sealed class LayerAdvanceRefusedMsg
{
	/// <summary>The host's own reason sentence, the same text it logged, so both sides read one wording.</summary>
	[ProtoMember(1)]
	public string? Reason { get; set; }
}
