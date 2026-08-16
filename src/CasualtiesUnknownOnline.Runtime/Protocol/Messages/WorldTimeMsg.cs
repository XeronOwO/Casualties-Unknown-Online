using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Host → guest: the authoritative world-time speed. Sent when the host's
/// speed changes, when a member (re)enters the world, and every 5 s as an
/// idempotent self-heal (direct <c>Time.timeScale</c> writers and local-only
/// effects can move a side away; the next broadcast brings it back).
/// </summary>
[ProtoContract]
public sealed class WorldTimeMsg
{
	[ProtoMember(1)]
	public WorldTimeSpeed Speed { get; set; }
}
