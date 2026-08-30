using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Live player marker-color change. A guest reports its own change to the host;
/// the host stores it and relays to the other guests. A host change is
/// broadcast directly. Existing join/handshake messages also carry the color,
/// so a late joiner still receives the current roster colors without this
/// message.
/// </summary>
[ProtoContract]
public sealed class PlayerColorUpdateMsg
{
	/// <summary>The member whose color changed (the sender on the guest report,
	/// the host on a host broadcast).</summary>
	[ProtoMember(1)]
	public ulong SteamId { get; set; }

	/// <summary>True when <see cref="Color"/> carries the member's selected
	/// color. False = the member switched back to the automatic palette.</summary>
	[ProtoMember(2)]
	public bool HasColor { get; set; }

	/// <summary>The selected RGBA marker color (only meaningful when
	/// <see cref="HasColor"/> is true).</summary>
	[ProtoMember(3)]
	public NetColorRgbaMsg Color { get; set; } = new();
}
