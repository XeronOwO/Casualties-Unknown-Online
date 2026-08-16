using CasualtiesUnknownOnline.Abstractions;
using ProtoBuf;

namespace CasualtiesUnknownOnline.Runtime.Protocol.Messages;

/// <summary>
/// One member's declared mod, carried in the handshake (HandshakeMsg.Mods —
/// Phase 4 Mod API consistency check). The host validates the members' lists
/// against its own: missing/mismatched mods are rejected per the NetworkMode
/// policy (RequiresAllPlayers/Synchronized/Authoritative missing or version-
/// unequal → reject; ClientOnly/Cosmetic differences and host-only mods → pass).
/// The enum serializes as its underlying int; Unspecified (0) is an invalid
/// wire value — the host's shape check rejects it.
/// </summary>
[ProtoContract]
public sealed class ModInfoMsg
{
	[ProtoMember(1)]
	public string Id { get; set; } = string.Empty;

	/// <summary>SemVer version — state-bearing modes are compared by precedence (build metadata ignored).</summary>
	[ProtoMember(2)]
	public string Version { get; set; } = string.Empty;

	[ProtoMember(3)]
	public NetworkMode NetworkMode { get; set; }

	/// <summary>
	/// The declared permission flags (Phase 4b). Serialized as their underlying
	/// int; unknown bits are rejected by the host's shape check. State-bearing
	/// modes require the member's flags to equal the host's.
	/// </summary>
	[ProtoMember(4)]
	public ModPermission Permissions { get; set; }
}
