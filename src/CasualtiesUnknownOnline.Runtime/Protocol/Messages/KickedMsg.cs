using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Host → guest: the host removed this member from the session. The receiver
/// tears its session down immediately (no host migration in the MVP — the
/// kicked player returns to the menu / lobby).
/// </summary>
[ProtoContract]
public sealed class KickedMsg
{
	/// <summary>Short human-readable reason recorded in the host's log and surfaced by at least the kick target's own connection state.</summary>
	[ProtoMember(1)]
	public string Reason { get; set; } = "";
}
