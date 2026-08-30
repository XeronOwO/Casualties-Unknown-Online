using System.Collections.Generic;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// Guest → host: protocol version + local scene state + the guest's declared
/// mod list (Phase 4 Mod API consistency check — the host validates the
/// members' lists against its own before admitting them). The list is null
/// for an old client that never sends it — the host treats null as an empty
/// list (the protocol version gate rejects cross-version sessions anyway,
/// since this field is a behavioral change: ProtocolVersion 3).
/// </summary>
[ProtoContract]
public sealed class HandshakeMsg
{
	[ProtoMember(1)]
	public int Protocol { get; set; }

	[ProtoMember(2)]
	public SceneStateMsg Scene { get; set; } = new();

	[ProtoMember(3)]
	public List<ModInfoMsg>? Mods { get; set; }

	/// <summary>The sender's custom in-game display name. Used by IP-direct sessions;
	/// Steam sessions may send the Steam persona name as a harmless duplicate.</summary>
	[ProtoMember(4)]
	public string DisplayName { get; set; } = "";

	/// <summary>True when <see cref="Color"/> carries the sender's manually
	/// selected presentation color. False = the sender is on the automatic
	/// SteamId palette and every peer derives the same fallback color locally.</summary>
	[ProtoMember(5)]
	public bool HasColor { get; set; }

	/// <summary>The sender's selected RGBA marker color (only meaningful when
	/// <see cref="HasColor"/> is true).</summary>
	[ProtoMember(6)]
	public NetColorRgbaMsg Color { get; set; } = new();
}
