using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// A simple co-op text-chat message. One message shape serves both directions:
/// a guest reports its own chat to the host (SenderSteamId = the guest, the
/// transport sender must match on the host), and the host broadcasts the same
/// payload to the other members; a guest receiving a relay sees a non-local
/// SenderSteamId. The text is the final string, never re-derived.
/// </summary>
[ProtoContract]
public sealed class ChatMsg
{
	/// <summary>The actual author of the chat line (not the transport sender — on
	/// a host relay the transport sender is the host).</summary>
	[ProtoMember(1)]
	public ulong SenderSteamId { get; set; }

	[ProtoMember(2)]
	public string Text { get; set; } = "";
}
