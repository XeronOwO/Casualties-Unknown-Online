using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Host → guest: explicit "enter the world" instruction. Sent after the world
/// params (ordered, reliable) when the host is already in a world — at
/// handshake time or when the host enters the world — so the guest starts the
/// run only once the params it needs are in hand (the timing decision belongs
/// to the host, not to a guest-side pump racing the params arrival).
/// </summary>
[ProtoContract]
public sealed class WorldJoinMsg
{
}
