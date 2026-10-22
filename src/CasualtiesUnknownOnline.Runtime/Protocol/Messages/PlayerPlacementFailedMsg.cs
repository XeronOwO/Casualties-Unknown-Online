using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Receiving client → host: the destination it ANSWERED is no longer available on its live body (the receiver
/// picked something up between the answer and the transfer). It did not substitute — the host's row already
/// states the answered destination — so it reports the failure and creates nothing; the host returns the row and
/// publishes the return, and the item ends with the giver. This is the residual the placement design records.
/// </summary>
[ProtoContract]
public sealed class PlayerPlacementFailedMsg
{
	/// <summary>The item whose answered destination is no longer available.</summary>
	[ProtoMember(1)]
	public ulong ItemInstanceId { get; set; }

	/// <summary>The receiving client's own reason, in its own words.</summary>
	[ProtoMember(2)]
	public string Reason { get; set; } = "";
}
