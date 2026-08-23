using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Host → guest: the host permanently banned this member from this host's
/// sessions. The receiver tears its session down immediately, like
/// <see cref="KickedMsg"/>, but the SteamID is also recorded on the host so
/// future handshakes are rejected before the member enters the roster.
/// </summary>
[ProtoContract]
public sealed class BannedMsg
{
	/// <summary>Short human-readable reason recorded in the host's log and surfaced by the target's connection state.</summary>
	[ProtoMember(1)]
	public string Reason { get; set; } = "";
}
