using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// ONE transient co-op location ping (first middle click = circle, quick second
/// middle click = exclamation). The pinger's own client already added the
/// marker locally; the host fires the received event on its own world and
/// relays to the other members (source excluded). It is pure presentation —
/// no authority, no persistent world state, and no snapshot fallback.
/// </summary>
[ProtoContract]
public sealed class LocationPingMsg
{
	/// <summary>The SteamId of the player who placed the ping (stamped by the reporter).</summary>
	[ProtoMember(1)]
	public ulong SenderSteamId { get; set; }

	/// <summary>The world-space position of the ping.</summary>
	[ProtoMember(2)]
	public NetVector2Msg Position { get; set; } = new();

	/// <summary>The marker visual: circle or exclamation/alert.</summary>
	[ProtoMember(3)]
	public LocationPingKind Kind { get; set; }
}
